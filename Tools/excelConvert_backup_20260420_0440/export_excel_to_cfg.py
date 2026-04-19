from __future__ import annotations

import json
import posixpath
import re
import sys
import zipfile
from dataclasses import dataclass
from pathlib import Path
import xml.etree.ElementTree as ET

MAIN_NS = {"a": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}
REL_NS = {"rel": "http://schemas.openxmlformats.org/package/2006/relationships"}
REL_ID_ATTR = "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id"
TEXT_TAG = "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}t"

META_PATTERN = re.compile(r"^\s*convert\(\s*([^,]+?)\s*,\s*([^,]+?)\s*,\s*([^)]+?)\s*\)\s*$")
FIELD_PATTERN = re.compile(r"^\s*(repeated\s+)?([A-Za-z_]\w*)\s+([A-Za-z_]\w*)\s*=\s*\d+\s*,?\s*$")


class ExportError(RuntimeError):
    pass


@dataclass(frozen=True)
class FieldDef:
    name: str
    scalar_type: str
    repeated: bool = False


@dataclass(frozen=True)
class SheetMeta:
    config_file: str
    output_file: str
    scheme_name: str


@dataclass(frozen=True)
class SheetData:
    name: str
    rows: list[list[str]]


class SchemaCache:
    def __init__(self) -> None:
        self._cache: dict[Path, dict[str, list[FieldDef]]] = {}

    def get_fields(self, schema_path: Path, scheme_name: str) -> list[FieldDef]:
        if schema_path not in self._cache:
            self._cache[schema_path] = parse_schema_file(schema_path)
        schemes = self._cache[schema_path]
        if scheme_name not in schemes:
            raise ExportError(f"schema 文件 {schema_path.name} 中找不到 {scheme_name}")
        return schemes[scheme_name]


def main(argv: list[str]) -> int:
    script_path = Path(__file__).resolve()
    repo_root = script_path.parents[2]
    excel_dir = repo_root / "common" / "excel" / "xls"
    schema_dir = repo_root / "common" / "cfg"
    output_dir = repo_root / "Assets" / "Cfg"

    workbook_paths = collect_workbooks(excel_dir, argv[1:])
    if not workbook_paths:
        raise ExportError("未找到可导出的 xlsx 文件")

    output_dir.mkdir(parents=True, exist_ok=True)
    schema_cache = SchemaCache()
    exported_count = 0

    for workbook_path in workbook_paths:
        exported_count += export_workbook(workbook_path, schema_dir, output_dir, schema_cache)

    print(f"[ExcelCfgExporter] 导出完成，共更新 {exported_count} 个配置文件。")
    return 0


def collect_workbooks(excel_dir: Path, raw_args: list[str]) -> list[Path]:
    if raw_args:
        paths: list[Path] = []
        for raw_arg in raw_args:
            arg_path = Path(raw_arg)
            if not arg_path.is_absolute():
                arg_path = excel_dir / raw_arg
            if arg_path.suffix.lower() != ".xlsx":
                arg_path = arg_path.with_suffix(".xlsx")
            if not arg_path.exists():
                raise ExportError(f"找不到工作簿: {arg_path}")
            if should_skip_workbook(arg_path):
                continue
            paths.append(arg_path)
        return sorted(paths)

    return sorted(
        path for path in excel_dir.glob("*.xlsx")
        if path.is_file() and not should_skip_workbook(path)
    )


def should_skip_workbook(path: Path) -> bool:
    name = path.name.lower()
    return name.startswith("~$") or name == "equipment_new.xlsx"


def export_workbook(workbook_path: Path, schema_dir: Path, output_dir: Path, schema_cache: SchemaCache) -> int:
    exported_count = 0
    for sheet in read_xlsx(workbook_path):
        if len(sheet.rows) < 2:
            continue

        meta = parse_sheet_meta(sheet.rows[0][0] if sheet.rows[0] else "")
        if meta is None:
            continue

        schema_path = schema_dir / meta.config_file
        if not schema_path.exists():
            raise ExportError(f"找不到 schema 文件: {schema_path}")

        fields = schema_cache.get_fields(schema_path, meta.scheme_name)
        headers = [value.strip() for value in sheet.rows[1]]
        output_path = output_dir / meta.output_file
        existing_records = load_existing_records(output_path, meta.scheme_name, fields[0].name if fields else None)
        header_index, missing_headers = build_header_index(workbook_path, sheet.name, headers, fields)
        records = build_records(workbook_path, sheet, fields, header_index, existing_records)

        payload = {meta.scheme_name: records}
        json_text = json.dumps(payload, ensure_ascii=True, indent=2) + "\n"
        output_path.write_text(json_text, encoding="utf-8")

        print(f"[ExcelCfgExporter] {workbook_path.name}::{sheet.name} -> {output_path.name}（{len(records)} 条）")
        if missing_headers:
            print(
                f"[ExcelCfgExporter] {workbook_path.name}::{sheet.name} 缺少表头 {', '.join(missing_headers)}，已使用旧配置或默认值回填"
            )
        exported_count += 1

    return exported_count


def build_header_index(workbook_path: Path, sheet_name: str, headers: list[str], fields: list[FieldDef]) -> tuple[dict[str, int], list[str]]:
    field_names = [field.name for field in fields]
    header_index: dict[str, int] = {}

    for index, header in enumerate(headers):
        if not header:
            continue
        if header in header_index:
            raise ExportError(f"{workbook_path.name}::{sheet_name} 表头重复: {header}")
        header_index[header] = index

    missing_headers = [name for name in field_names if name not in header_index]

    unexpected_headers = [name for name in header_index if name not in field_names]
    if unexpected_headers:
        raise ExportError(f"{workbook_path.name}::{sheet_name} 存在 schema 未定义的表头: {', '.join(unexpected_headers)}")

    return header_index, missing_headers


def build_records(
    workbook_path: Path,
    sheet: SheetData,
    fields: list[FieldDef],
    header_index: dict[str, int],
    existing_records: dict[str, dict[str, object]],
) -> list[dict[str, object]]:
    records: list[dict[str, object]] = []
    key_field = fields[0].name if fields else None

    for row_number, row in enumerate(sheet.rows[2:], start=3):
        if is_blank_row(row):
            continue

        existing_record = existing_records.get(read_key_value(row, header_index, key_field)) if key_field else None
        record: dict[str, object] = {}
        for field in fields:
            if field.name in header_index:
                index = header_index[field.name]
                raw_value = row[index] if index < len(row) else ""
                record[field.name] = convert_field_value(
                    raw_value=raw_value,
                    field=field,
                    workbook_name=workbook_path.name,
                    sheet_name=sheet.name,
                    row_number=row_number,
                )
                continue

            record[field.name] = get_fallback_field_value(existing_record, field)
        records.append(record)

    return records


def parse_sheet_meta(value: str) -> SheetMeta | None:
    value = value.strip()
    if not value:
        return None

    match = META_PATTERN.match(value)
    if not match:
        return None

    return SheetMeta(
        config_file=match.group(1).strip(),
        output_file=match.group(2).strip(),
        scheme_name=match.group(3).strip(),
    )


def load_existing_records(output_path: Path, scheme_name: str, key_field: str | None) -> dict[str, dict[str, object]]:
    if key_field is None or not output_path.exists():
        return {}

    try:
        payload = json.loads(output_path.read_text(encoding="utf-8-sig"))
    except Exception:
        return {}

    rows = payload.get(scheme_name)
    if not isinstance(rows, list):
        return {}

    record_map: dict[str, dict[str, object]] = {}
    for row in rows:
        if not isinstance(row, dict):
            continue
        key_value = row.get(key_field)
        if key_value is None:
            continue
        record_map[str(key_value)] = row
    return record_map


def read_key_value(row: list[str], header_index: dict[str, int], key_field: str | None) -> str:
    if key_field is None or key_field not in header_index:
        return ""
    index = header_index[key_field]
    return row[index].strip() if index < len(row) else ""


def get_fallback_field_value(existing_record: dict[str, object] | None, field: FieldDef) -> object:
    if existing_record is not None and field.name in existing_record:
        return existing_record[field.name]
    if field.repeated:
        return []
    return ""


def convert_field_value(*, raw_value: str, field: FieldDef, workbook_name: str, sheet_name: str, row_number: int) -> object:

    raw_value = raw_value.strip()
    try:
        if field.repeated:
            return parse_repeated_value(raw_value, field.scalar_type)
        return parse_scalar_value(raw_value, field.scalar_type)
    except ValueError as exc:
        raise ExportError(
            f"{workbook_name}::{sheet_name} 第 {row_number} 行字段 {field.name} 值非法: {raw_value}"
        ) from exc


def parse_scalar_value(raw_value: str, scalar_type: str) -> str:
    if raw_value == "":
        return ""

    if scalar_type == "string":
        return raw_value
    if scalar_type == "int32":
        return str(int(float(raw_value)))
    if scalar_type == "float":
        return normalize_float_text(raw_value)
    if scalar_type == "bool":
        lowered = raw_value.lower()
        if lowered in {"1", "true", "yes"}:
            return "True"
        if lowered in {"0", "false", "no"}:
            return "False"
        raise ValueError(raw_value)

    raise ExportError(f"暂不支持的标量类型: {scalar_type}")


def parse_repeated_value(raw_value: str, scalar_type: str) -> list[object]:
    if raw_value == "":
        return []

    parts = [part.strip() for part in re.split(r"[,|，；;、]+", raw_value) if part.strip()]
    if scalar_type == "string":
        return parts
    if scalar_type == "int32":
        return [int(float(part)) for part in parts]
    if scalar_type == "float":
        return [float(part) for part in parts]
    if scalar_type == "bool":
        result: list[bool] = []
        for part in parts:
            lowered = part.lower()
            if lowered in {"1", "true", "yes"}:
                result.append(True)
            elif lowered in {"0", "false", "no"}:
                result.append(False)
            else:
                raise ValueError(part)
        return result

    raise ExportError(f"暂不支持的 repeated 类型: {scalar_type}")


def normalize_float_text(raw_value: str) -> str:
    lowered = raw_value.lower()
    if "e" in lowered:
        value = float(raw_value)
        return format(value, "g")

    if "." not in raw_value:
        float(raw_value)
        return raw_value

    value = float(raw_value)
    normalized = raw_value.rstrip("0").rstrip(".")
    if normalized in {"", "-", "+"}:
        return "0"
    if normalized == "-0":
        return "0"
    if normalized.endswith("."):
        return normalized[:-1]
    if normalized:
        return normalized
    return format(value, "g")


def is_blank_row(row: list[str]) -> bool:
    return all(cell.strip() == "" for cell in row)


def parse_schema_file(schema_path: Path) -> dict[str, list[FieldDef]]:
    schemes: dict[str, list[FieldDef]] = {}
    current_scheme: str | None = None
    current_fields: list[FieldDef] = []

    for raw_line in schema_path.read_text(encoding="utf-8-sig").splitlines():

        line = raw_line.split("//", 1)[0].strip()
        if not line:
            continue

        if current_scheme is None:
            if line.endswith("{"):
                current_scheme = line[:-1].strip()
                current_fields = []
            continue

        if line == "}":
            schemes[current_scheme] = current_fields
            current_scheme = None
            current_fields = []
            continue

        match = FIELD_PATTERN.match(line)
        if not match:
            raise ExportError(f"schema 解析失败: {schema_path.name} -> {line}")

        current_fields.append(
            FieldDef(
                name=match.group(3),
                scalar_type=match.group(2),
                repeated=bool(match.group(1)),
            )
        )

    if current_scheme is not None:
        raise ExportError(f"schema 文件未正确闭合: {schema_path.name}")

    return schemes


def read_xlsx(workbook_path: Path) -> list[SheetData]:
    with zipfile.ZipFile(workbook_path) as zf:
        shared_strings = read_shared_strings(zf)
        sheet_refs = read_sheet_refs(zf)
        return [
            SheetData(name=sheet_name, rows=read_sheet_rows(zf, target_path, shared_strings))
            for sheet_name, target_path in sheet_refs
        ]


def read_shared_strings(zf: zipfile.ZipFile) -> list[str]:
    if "xl/sharedStrings.xml" not in zf.namelist():
        return []

    root = ET.fromstring(zf.read("xl/sharedStrings.xml"))
    strings: list[str] = []
    for item in root.findall("a:si", MAIN_NS):
        strings.append("".join(node.text or "" for node in item.iter(TEXT_TAG)))
    return strings


def read_sheet_refs(zf: zipfile.ZipFile) -> list[tuple[str, str]]:
    workbook_root = ET.fromstring(zf.read("xl/workbook.xml"))
    rels_root = ET.fromstring(zf.read("xl/_rels/workbook.xml.rels"))
    rel_map = {
        rel.attrib["Id"]: normalize_zip_path(rel.attrib["Target"])
        for rel in rels_root.findall("rel:Relationship", REL_NS)
    }

    sheet_refs: list[tuple[str, str]] = []
    sheets_root = workbook_root.find("a:sheets", MAIN_NS)
    if sheets_root is None:
        return sheet_refs

    for sheet in sheets_root.findall("a:sheet", MAIN_NS):
        rel_id = sheet.attrib.get(REL_ID_ATTR)
        if not rel_id or rel_id not in rel_map:
            continue
        sheet_refs.append((sheet.attrib.get("name", ""), rel_map[rel_id]))

    return sheet_refs


def normalize_zip_path(target: str) -> str:
    normalized = target.replace("\\", "/").lstrip("/")
    if not normalized.startswith("xl/"):
        normalized = f"xl/{normalized}"
    return posixpath.normpath(normalized)


def read_sheet_rows(zf: zipfile.ZipFile, sheet_path: str, shared_strings: list[str]) -> list[list[str]]:
    root = ET.fromstring(zf.read(sheet_path))
    sheet_data = root.find("a:sheetData", MAIN_NS)
    if sheet_data is None:
        return []

    rows: list[list[str]] = []
    for row_node in sheet_data.findall("a:row", MAIN_NS):
        row_values: dict[int, str] = {}
        max_index = -1

        for cell in row_node.findall("a:c", MAIN_NS):
            ref = cell.attrib.get("r", "")
            column_index = column_index_from_ref(ref) if ref else max_index + 1
            row_values[column_index] = read_cell_value(cell, shared_strings)
            max_index = max(max_index, column_index)

        if max_index < 0:
            rows.append([])
            continue

        row = [""] * (max_index + 1)
        for index, value in row_values.items():
            row[index] = value

        while row and row[-1] == "":
            row.pop()
        rows.append(row)

    return rows


def column_index_from_ref(cell_ref: str) -> int:
    column = 0
    for char in cell_ref:
        if not char.isalpha():
            break
        column = column * 26 + (ord(char.upper()) - ord("A") + 1)
    return max(column - 1, 0)


def read_cell_value(cell: ET.Element, shared_strings: list[str]) -> str:
    cell_type = cell.attrib.get("t")
    value_node = cell.find("a:v", MAIN_NS)

    if cell_type == "s" and value_node is not None:
        index = int(value_node.text or "0")
        return shared_strings[index] if 0 <= index < len(shared_strings) else ""

    if cell_type == "inlineStr":
        inline_node = cell.find("a:is", MAIN_NS)
        if inline_node is None:
            return ""
        return "".join(node.text or "" for node in inline_node.iter(TEXT_TAG))

    if cell_type == "b" and value_node is not None:
        return "True" if (value_node.text or "").strip() in {"1", "true", "TRUE"} else "False"

    if value_node is not None and value_node.text is not None:
        return value_node.text

    return ""


if __name__ == "__main__":
    try:
        raise SystemExit(main(sys.argv))
    except ExportError as exc:
        print(f"[ExcelCfgExporter] 错误: {exc}", file=sys.stderr)
        raise SystemExit(1)
