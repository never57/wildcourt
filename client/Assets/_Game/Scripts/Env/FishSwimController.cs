using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Env
{
    /// <summary>
    /// 游动鱼控制：水平巡游 + 边界/随机掉头 + 垂直正弦浮动，驱动 Animator 的 Speed 参数。
    /// 所有随机使用实例级 System.Random；不越出父级（FishLayer）矩形。
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(Image), typeof(Animator))]
    public class FishSwimController : MonoBehaviour
    {
        [SerializeField] float minSpeed = 20f;       // 水平 px/s
        [SerializeField] float maxSpeed = 60f;       // 水平 px/s
        [SerializeField] float turnDuration = 0.2f;  // 掉头缓动时长
        [SerializeField] float floatAmplitude = 6f;  // 垂直正弦浮动幅度 px
        [SerializeField] float floatFrequency = 0.8f; // Hz

        private RectTransform _rt;
        private RectTransform _parent;
        private Image _image;
        private Animator _animator;
        private System.Random _rng;

        private float _speed;         // 当前水平速度（正=向右）
        private float _targetSpeed;   // 巡游目标速度
        private float _turnTimer;     // 掉头过渡计时
        private float _baseY;         // 基线 Y（垂直浮动中心）
        private float _randomSeed;    // 浮动相位
        private bool _turning;
        private bool _initialized;

        public bool IsInitialized => _initialized;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _image = GetComponent<Image>();
            _animator = GetComponent<Animator>();
            _parent = _rt.parent as RectTransform;
            _rng = new System.Random(GetInstanceID());

            if (_image != null) _image.raycastTarget = false;

            if (!_initialized)
                InitializeDefault();
        }

        private void OnEnable()
        {
            _baseY = _rt.anchoredPosition.y;
            _randomSeed = (float)(_rng != null ? _rng.NextDouble() : 0) * Mathf.PI * 2f;
        }

        /// <summary>由生成器调用：设置精灵、缩放、透明度并随机初始朝向/速度/深度。</summary>
        public void Initialize(Sprite sprite, float scale, byte alpha)
        {
            if (_image == null) _image = GetComponent<Image>();
            if (_rt == null) _rt = GetComponent<RectTransform>();
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_rng == null) _rng = new System.Random(GetInstanceID());

            if (sprite != null && _image != null)
                _image.sprite = sprite;

            if (_image != null)
            {
                var c = _image.color;
                c.a = alpha / 255f;
                _image.color = c;
            }

            // 随机朝向：scaleX 符号与速度方向一致（鱼头朝运动方向，素材默认朝右）
            float dir = _rng.Next(2) == 0 ? 1f : -1f;
            _rt.localScale = new Vector3(scale * dir, scale, 1f);
            _speed = PickRandomSpeed() * dir;
            _targetSpeed = _speed;
            _turning = false;
            _turnTimer = 0f;
            _initialized = true;
        }

        private void InitializeDefault()
        {
            if (_rng == null) _rng = new System.Random(GetInstanceID());
            float s = 0.6f + (float)_rng.NextDouble() * 0.8f; // 0.6~1.4
            Initialize(_image != null ? _image.sprite : null, s, (byte)(150 + _rng.Next(106))); // 150~255
        }

        private void Update()
        {
            if (_rt == null || _parent == null) return;
            if (!_initialized) return;

            float dt = Time.deltaTime;
            UpdateTurn(dt);
            UpdateFloat(dt);

            Vector2 pos = _rt.anchoredPosition;
            pos.x += _speed * dt;

            // 边界检测：不越出 FishLayer 矩形
            float halfW = _rt.rect.width * Mathf.Abs(_rt.localScale.x) * 0.5f;
            float halfH = _rt.rect.height * Mathf.Abs(_rt.localScale.y) * 0.5f;
            float minX = _parent.rect.xMin + halfW;
            float maxX = _parent.rect.xMax - halfW;
            float minY = _parent.rect.yMin + halfH;
            float maxY = _parent.rect.yMax - halfH;

            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            _rt.anchoredPosition = pos;

            // 到达边界 → 掉头
            if ((_speed > 0f && pos.x >= maxX) || (_speed < 0f && pos.x <= minX))
                StartTurn();
            // 随机掉头：每秒 5% 概率
            else if ((float)_rng.NextDouble() < 0.05f * dt)
                StartTurn();

            // 归一化速度驱动动画
            if (_animator != null)
                _animator.SetFloat("Speed", Mathf.Abs(_speed) / maxSpeed);
        }

        /// <summary>掉头：localScale.x 符号翻转，turnDuration 内平滑过渡，期间速度降为 30%。</summary>
        private void StartTurn()
        {
            if (_turning) return;
            _turning = true;
            _turnTimer = 0f;
            _targetSpeed = -_speed;
        }

        private void UpdateTurn(float dt)
        {
            if (!_turning) return;
            _turnTimer += dt;
            float t = Mathf.Clamp01(_turnTimer / turnDuration);

            // 速度由当前向目标线性插值，过渡期间减速到 30%
            float v = Mathf.Lerp(_speed, _targetSpeed, t);
            _speed = v * (t < 1f ? 0.3f + 0.7f * t : 1f);

            // scale.x 平滑翻转（禁止瞬翻）：从当前符号过渡到目标符号
            float targetScaleX = _targetSpeed >= 0f ? Mathf.Abs(_rt.localScale.x) : -Mathf.Abs(_rt.localScale.x);
            _rt.localScale = new Vector3(Mathf.Lerp(_rt.localScale.x, targetScaleX, t), _rt.localScale.y, _rt.localScale.z);

            if (t >= 1f)
            {
                _speed = _targetSpeed;
                _turning = false;
            }
        }

        private void UpdateFloat(float dt)
        {
            float y = _baseY + Mathf.Sin(Time.time * floatFrequency * Mathf.PI * 2f + _randomSeed) * floatAmplitude;
            var pos = _rt.anchoredPosition;
            pos.y = y;
            _rt.anchoredPosition = pos;
        }

        private float PickRandomSpeed()
        {
            return minSpeed + (float)_rng.NextDouble() * (maxSpeed - minSpeed);
        }

        // ===== 自检采样接口 =====
        public float CurrentSpeed => _speed;
        public float ScaleX => _rt != null ? _rt.localScale.x : 0f;
    }
}
