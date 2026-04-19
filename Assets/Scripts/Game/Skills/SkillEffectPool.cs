using System;
using System.Collections;
using System.Collections.Generic;
using POELike.ECS.Components;
using UnityEngine;

namespace POELike.Game.Skills
{
    /// <summary>
    /// 技能特效池。
    /// 负责按技能配置中的特效名预加载与复用特效实例。
    /// </summary>
    public sealed class SkillEffectPool : MonoBehaviour
    {
        private const string EffectResourcesRoot = "Effects/Skills";
        private const int DefaultPrewarmCount = 1;

        private static SkillEffectPool s_instance;

        private readonly Dictionary<string, Queue<PooledSkillEffect>> _availableByKey =
            new Dictionary<string, Queue<PooledSkillEffect>>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, GameObject> _prefabsByKey =
            new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, Transform> _poolRootsByKey =
            new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> _warnedMissingPrefabs =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static void EnsureInitialized()
        {
            _ = Instance;
        }

        public static void PreloadConfiguredEffects()
        {
            Instance.InternalPreloadConfiguredEffects();
        }

        public static void PreloadSkillEffects(IEnumerable<SkillSlot> skillSlots)
        {
            Instance.InternalPreloadSkillEffects(skillSlots);
        }

        public static void PreloadEffect(string effectName, int initialCount = DefaultPrewarmCount)
        {
            Instance.InternalPreloadEffect(effectName, initialCount);
        }

        public static void PlaySkillEffect(SkillData skill, Vector3 casterPosition, Vector3 targetPosition)
        {
            if (skill == null || string.IsNullOrWhiteSpace(skill.SkillEffectName))
                return;

            Instance.InternalPlaySkillEffect(skill, casterPosition, targetPosition);
        }

        private static SkillEffectPool Instance
        {
            get
            {
                if (s_instance != null)
                    return s_instance;

                var root = new GameObject("SkillEffectPool");
                DontDestroyOnLoad(root);
                s_instance = root.AddComponent<SkillEffectPool>();
                return s_instance;
            }
        }

        private void InternalPreloadConfiguredEffects()
        {
            var configs = SkillConfigLoader.ActiveSkills;
            for (int i = 0; i < configs.Count; i++)
            {
                var config = configs[i];
                if (config == null || string.IsNullOrWhiteSpace(config.SkillEffectName))
                    continue;

                InternalPreloadEffect(config.SkillEffectName, DefaultPrewarmCount);
            }
        }

        private void InternalPreloadSkillEffects(IEnumerable<SkillSlot> skillSlots)
        {
            if (skillSlots == null)
                return;

            foreach (var slot in skillSlots)
            {
                var effectName = slot?.SkillData?.SkillEffectName;
                if (string.IsNullOrWhiteSpace(effectName))
                    continue;

                InternalPreloadEffect(effectName, DefaultPrewarmCount);
            }
        }

        private void InternalPreloadEffect(string effectName, int initialCount)
        {
            string key = NormalizeEffectKey(effectName);
            if (string.IsNullOrWhiteSpace(key))
                return;

            EnsurePool(key);
            var queue = _availableByKey[key];
            int targetCount = Mathf.Max(1, initialCount);
            while (queue.Count < targetCount)
                queue.Enqueue(CreateInstance(key));
        }

        private void InternalPlaySkillEffect(SkillData skill, Vector3 casterPosition, Vector3 targetPosition)
        {
            string key = NormalizeEffectKey(skill.SkillEffectName);
            if (string.IsNullOrWhiteSpace(key))
                return;

            EnsurePool(key);
            var queue = _availableByKey[key];
            var effect = queue.Count > 0 ? queue.Dequeue() : CreateInstance(key);
            if (effect == null)
                return;

            Vector3 spawnPosition = ResolveSpawnPosition(skill, casterPosition, targetPosition);
            Quaternion spawnRotation = ResolveSpawnRotation(casterPosition, targetPosition, spawnPosition);

            var effectTransform = effect.transform;
            effectTransform.SetParent(transform, false);
            effectTransform.position = spawnPosition;
            effectTransform.rotation = spawnRotation;
            effect.Play();
        }

        internal void ReturnToPool(PooledSkillEffect effect)
        {
            if (effect == null || string.IsNullOrWhiteSpace(effect.EffectKey))
                return;

            EnsurePool(effect.EffectKey);

            effect.gameObject.SetActive(false);
            effect.transform.SetParent(_poolRootsByKey[effect.EffectKey], false);
            _availableByKey[effect.EffectKey].Enqueue(effect);
        }

        private void EnsurePool(string key)
        {
            if (!_prefabsByKey.ContainsKey(key))
                _prefabsByKey[key] = LoadEffectPrefab(key);

            if (!_availableByKey.ContainsKey(key))
                _availableByKey[key] = new Queue<PooledSkillEffect>();

            if (!_poolRootsByKey.ContainsKey(key))
            {
                var root = new GameObject($"{key}_Pool").transform;
                root.SetParent(transform, false);
                _poolRootsByKey[key] = root;
            }
        }

        private PooledSkillEffect CreateInstance(string key)
        {
            EnsurePool(key);
            var prefab = _prefabsByKey[key];
            if (prefab == null)
                return null;

            var go = Instantiate(prefab, _poolRootsByKey[key], false);
            go.name = $"{key}_Instance";
            go.SetActive(false);

            var pooled = go.GetComponent<PooledSkillEffect>();
            if (pooled == null)
                pooled = go.AddComponent<PooledSkillEffect>();

            pooled.Bind(this, key);
            return pooled;
        }

        private GameObject LoadEffectPrefab(string key)
        {
            string resourcePath = BuildResourcesPath(key);
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab != null)
                return prefab;

            if (_warnedMissingPrefabs.Add(key))
                Debug.LogWarning($"[SkillEffectPool] 未找到技能特效预制体 Resources/{resourcePath}，将使用运行时占位特效。后续补上同名 prefab 后会自动切换。");

            return CreateFallbackEffectTemplate(key);
        }

        private static string NormalizeEffectKey(string effectName)
        {
            return string.IsNullOrWhiteSpace(effectName) ? null : effectName.Trim();
        }

        private static string BuildResourcesPath(string key)
        {
            return key.IndexOf('/') >= 0 ? key : $"{EffectResourcesRoot}/{key}";
        }

        private static Vector3 ResolveSpawnPosition(SkillData skill, Vector3 casterPosition, Vector3 targetPosition)
        {
            if (skill == null)
                return casterPosition;

            switch (skill.Type)
            {
                case SkillType.AoE:
                case SkillType.Movement:
                    return targetPosition;
                default:
                    return casterPosition;
            }
        }

        private static Quaternion ResolveSpawnRotation(Vector3 casterPosition, Vector3 targetPosition, Vector3 spawnPosition)
        {
            Vector3 dir = targetPosition - casterPosition;
            if (dir.sqrMagnitude <= 0.0001f)
                dir = targetPosition - spawnPosition;
            if (dir.sqrMagnitude <= 0.0001f)
                dir = Vector3.forward;

            return Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        private GameObject CreateFallbackEffectTemplate(string key)
        {
            var template = new GameObject($"{key}_Template");
            template.transform.SetParent(transform, false);
            template.SetActive(false);

            var ps = template.AddComponent<ParticleSystem>();
            var renderer = template.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            ConfigureFallbackParticle(key, ps);
            return template;
        }

        private static void ConfigureFallbackParticle(string key, ParticleSystem particleSystem)
        {
            if (particleSystem == null)
                return;

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var color = ResolveFallbackColor(key);
            string lowered = key.ToLowerInvariant();

            var main = particleSystem.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = lowered.Contains("cyclone") ? 0.45f : 0.35f;
            main.startLifetime = lowered.Contains("blink") ? 0.25f : 0.45f;
            main.startSpeed = lowered.Contains("nova") ? 0.8f : 2.2f;
            main.startSize = lowered.Contains("cyclone") ? 0.35f : 0.28f;
            main.startColor = color;
            main.maxParticles = 48;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)(lowered.Contains("nova") ? 30 : 18))
            });

            var shape = particleSystem.shape;
            shape.enabled = true;
            if (lowered.Contains("nova"))
            {
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.65f;
            }
            else if (lowered.Contains("blink"))
            {
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = 0.4f;
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = lowered.Contains("cyclone") ? 35f : 12f;
                shape.radius = lowered.Contains("cyclone") ? 0.45f : 0.1f;
            }

            var colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(Color.Lerp(color, Color.white, 0.35f), 0.6f),
                    new GradientColorKey(color, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.6f, 0.65f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.25f));
        }

        private static Color ResolveFallbackColor(string key)
        {
            string lowered = key.ToLowerInvariant();
            if (lowered.Contains("fire"))
                return new Color(1.00f, 0.42f, 0.12f, 1f);
            if (lowered.Contains("frost") || lowered.Contains("ice"))
                return new Color(0.35f, 0.78f, 1.00f, 1f);
            if (lowered.Contains("blink"))
                return new Color(0.75f, 0.45f, 1.00f, 1f);
            if (lowered.Contains("lightning"))
                return new Color(1.00f, 0.92f, 0.35f, 1f);
            if (lowered.Contains("heavy") || lowered.Contains("attack"))
                return new Color(1.00f, 0.95f, 0.95f, 1f);
            if (lowered.Contains("cyclone"))
                return new Color(0.92f, 0.92f, 0.92f, 1f);

            return new Color(0.90f, 0.72f, 1.00f, 1f);
        }

        private void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
        }
    }

    public sealed class PooledSkillEffect : MonoBehaviour
    {
        private const float DefaultLifetime = 0.65f;

        private readonly List<ParticleSystem> _particleSystems = new List<ParticleSystem>(4);

        private SkillEffectPool _owner;
        private Coroutine _returnRoutine;

        public string EffectKey { get; private set; }

        public void Bind(SkillEffectPool owner, string effectKey)
        {
            _owner = owner;
            EffectKey = effectKey;
            CacheParticleSystems();
        }

        public void Play()
        {
            CacheParticleSystems();

            gameObject.SetActive(true);

            if (_returnRoutine != null)
                StopCoroutine(_returnRoutine);

            for (int i = 0; i < _particleSystems.Count; i++)
            {
                var ps = _particleSystems[i];
                if (ps == null)
                    continue;

                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }

            _returnRoutine = StartCoroutine(ReturnAfterDelay());
        }

        private IEnumerator ReturnAfterDelay()
        {
            yield return new WaitForSeconds(CalculateLifetime());
            _owner?.ReturnToPool(this);
        }

        private void CacheParticleSystems()
        {
            _particleSystems.Clear();
            GetComponentsInChildren(true, _particleSystems);
        }

        private float CalculateLifetime()
        {
            float lifetime = DefaultLifetime;
            for (int i = 0; i < _particleSystems.Count; i++)
            {
                var ps = _particleSystems[i];
                if (ps == null)
                    continue;

                var main = ps.main;
                float startLifetime = main.startLifetime.constantMax;
                if (main.startLifetime.mode == ParticleSystemCurveMode.Constant)
                    startLifetime = main.startLifetime.constant;
                else if (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants)
                    startLifetime = Mathf.Max(main.startLifetime.constantMin, main.startLifetime.constantMax);
                else if (startLifetime <= 0f)
                    startLifetime = DefaultLifetime * 0.5f;

                lifetime = Mathf.Max(lifetime, main.duration + startLifetime);
            }

            return lifetime + 0.1f;
        }
    }
}