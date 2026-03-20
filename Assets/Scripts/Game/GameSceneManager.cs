using UnityEngine;
using UnityEngine.SceneManagement;
using POELike.Game.Character;
using POELike.Game.UI;
using POELike.Managers;

namespace POELike.Game
{
    /// <summary>
    /// 游戏场景管理器
    /// 挂载在 GameScene 的 GameSceneManager GameObject 上
    /// 负责：构建3D场景环境 → 生成玩家 → 启动摄像机跟随
    /// </summary>
    public class GameSceneManager : MonoBehaviour
    {
        [Header("玩家设置")]
        [SerializeField] private Vector3 _playerSpawnPoint = new Vector3(0f, 0.5f, 0f);

        [Header("场景环境（运行时自动生成，可留空）")]
        [SerializeField] private bool _autoGenerateEnvironment = true;

        // 运行时引用
        private PlayerController _playerController;
        private CameraController _cameraController;

        // ── 生命周期 ──────────────────────────────────────────────────

        private void Start()
        {
            var data = SceneLoader.PendingCharacterData;
            if (data == null)
            {
                // 编辑器直接运行 GameScene 时使用默认数据
                data = new CharacterSaveData("debug", "调试角色", 1, "本地");
                Debug.LogWarning("[GameSceneManager] 未检测到存档数据，使用默认调试角色");
            }

            Debug.Log($"[GameSceneManager] 场景初始化，角色：{data.CharacterName}  Lv.{data.Level}");

            if (_autoGenerateEnvironment)
                BuildEnvironment();

            SpawnPlayer(data);
        }

        // ── 场景环境构建 ──────────────────────────────────────────────

        /// <summary>
        /// 程序化构建基础3D场景：地面、边界墙、灯光
        /// </summary>
        private void BuildEnvironment()
        {
            // ── 地面 ──────────────────────────────────────────────────
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10f, 1f, 10f); // 100×100 单位
            ground.transform.position   = Vector3.zero;
            SetMaterialColor(ground, new Color(0.25f, 0.22f, 0.18f)); // 深褐色地面

            // 地面物理层
            ground.layer = LayerMask.NameToLayer("Default");

            // ── 方向光 ────────────────────────────────────────────────
            var sunGo = new GameObject("Sun");
            var sun   = sunGo.AddComponent<Light>();
            sun.type      = LightType.Directional;
            sun.color     = new Color(1f, 0.95f, 0.8f);
            sun.intensity = 1.2f;
            sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // ── 环境光 ────────────────────────────────────────────────
            RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.15f, 0.2f);

            // ── 场景装饰：几个石柱 ────────────────────────────────────
            SpawnDecoration();

            Debug.Log("[GameSceneManager] 场景环境构建完成");
        }

        /// <summary>
        /// 生成场景装饰物（石柱）
        /// </summary>
        private void SpawnDecoration()
        {
            var positions = new Vector3[]
            {
                new Vector3( 8f, 1f,  8f),
                new Vector3(-8f, 1f,  8f),
                new Vector3( 8f, 1f, -8f),
                new Vector3(-8f, 1f, -8f),
                new Vector3(15f, 1f,  0f),
                new Vector3(-15f,1f,  0f),
            };

            foreach (var pos in positions)
            {
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = "Pillar";
                pillar.transform.position   = pos;
                pillar.transform.localScale = new Vector3(1f, 2f, 1f);
                SetMaterialColor(pillar, new Color(0.4f, 0.38f, 0.35f));
            }
        }

        /// <summary>
        /// 设置 Primitive 的材质颜色（URP 兼容）
        /// </summary>
        private void SetMaterialColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            // 创建独立材质实例，避免共享材质被修改
            var mat = new Material(renderer.sharedMaterial);
            mat.color = color;
            renderer.material = mat;
        }

        // ── 玩家生成 ──────────────────────────────────────────────────

        /// <summary>
        /// 在出生点生成玩家 GameObject，并由 PlayerController 创建 ECS Entity
        /// </summary>
        private void SpawnPlayer(CharacterSaveData data)
        {
            // 创建玩家 GameObject
            var playerGo = new GameObject($"Player_{data.CharacterName}");
            playerGo.transform.position = _playerSpawnPoint;

            // 添加 CharacterController（PlayerController 的 RequireComponent）
            var cc = playerGo.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            // 添加玩家视觉（胶囊体）
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(playerGo.transform);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.transform.localScale    = Vector3.one;
            SetMaterialColor(visual, new Color(0.2f, 0.5f, 0.9f)); // 蓝色玩家
            // 移除 Capsule 自带的 Collider（由 CharacterController 负责碰撞）
            Destroy(visual.GetComponent<CapsuleCollider>());

            // 添加 PlayerController 并传入存档数据
            _playerController = playerGo.AddComponent<PlayerController>();
            _playerController.InitFromSaveData(data);

            // 设置摄像机跟随
            SetupCamera(playerGo.transform);

            Debug.Log($"[GameSceneManager] 玩家已生成：{data.CharacterName}  位置：{_playerSpawnPoint}");
        }

        /// <summary>
        /// 设置等距跟随摄像机
        /// </summary>
        private void SetupCamera(Transform target)
        {
            var camGo = new GameObject("GameCamera");
            _cameraController = camGo.AddComponent<CameraController>();
            _cameraController.SetTarget(target);

            // 将主摄像机组件移到 GameCamera（或直接使用 Camera.main）
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.SetParent(null);
                _cameraController.AssignCamera(cam);
            }
            else
            {
                var newCam = camGo.AddComponent<Camera>();
                newCam.clearFlags       = CameraClearFlags.Skybox;
                newCam.fieldOfView      = 60f;
                newCam.nearClipPlane    = 0.1f;
                newCam.farClipPlane     = 500f;
                newCam.tag              = "MainCamera";
            }
        }
    }
}
