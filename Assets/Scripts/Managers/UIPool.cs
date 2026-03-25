using System.Collections.Generic;
using UnityEngine;

namespace POELike.Managers
{
    /// <summary>
    /// UI 对象池。
    /// 以资源路径（Resources 相对路径）为 key，缓存已实例化但暂时不用的 UI GameObject。
    /// 由 <see cref="UIManager"/> 统一调用，外部一般不直接使用。
    /// </summary>
    public class UIPool
    {
        // key = Resources 路径，value = 闲置对象栈
        private readonly Dictionary<string, Stack<GameObject>> _pool
            = new Dictionary<string, Stack<GameObject>>();

        // 池根节点（隐藏，DontDestroyOnLoad）
        private readonly Transform _poolRoot;

        public UIPool(Transform poolRoot)
        {
            _poolRoot = poolRoot;
        }

        // ── 公开接口 ──────────────────────────────────────────────────

        /// <summary>
        /// 从池中取出一个对象。若池中没有则返回 null。
        /// 取出后对象会被移出池根节点，由调用方重新设置父节点。
        /// </summary>
        public GameObject Get(string path)
        {
            if (!_pool.TryGetValue(path, out var stack) || stack.Count == 0)
                return null;

            var go = stack.Pop();
            if (go == null)
            {
                // 对象已被外部销毁，递归再取一次
                return Get(path);
            }

            go.SetActive(true);
            Debug.Log($"[UIPool] 从池中取出：{path}（剩余 {stack.Count}）");
            return go;
        }

        /// <summary>
        /// 将对象归还到池中。对象会被隐藏并挂到池根节点下。
        /// </summary>
        public void Return(string path, GameObject go)
        {
            if (go == null) return;

            go.SetActive(false);
            go.transform.SetParent(_poolRoot, false);

            if (!_pool.TryGetValue(path, out var stack))
            {
                stack = new Stack<GameObject>();
                _pool[path] = stack;
            }

            stack.Push(go);
            Debug.Log($"[UIPool] 归还到池：{path}（当前 {stack.Count}）");
        }

        /// <summary>池中是否存在可用对象</summary>
        public bool Has(string path)
        {
            return _pool.TryGetValue(path, out var stack) && stack.Count > 0;
        }

        /// <summary>销毁并清空整个池</summary>
        public void Clear()
        {
            foreach (var kv in _pool)
            {
                foreach (var go in kv.Value)
                {
                    if (go != null)
                        Object.Destroy(go);
                }
            }
            _pool.Clear();
            Debug.Log("[UIPool] 已清空");
        }

        /// <summary>销毁并清空指定路径的缓存</summary>
        public void Clear(string path)
        {
            if (!_pool.TryGetValue(path, out var stack)) return;
            foreach (var go in stack)
            {
                if (go != null)
                    Object.Destroy(go);
            }
            _pool.Remove(path);
        }
    }
}
