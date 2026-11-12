using Il2Cpp;
using UnityEngine;

namespace TrainerByEMPEROR
{
    // Режим полёта: отключаем контроллер игрока (гравитацию и столкновения)
    // и сами двигаем персонажа в сторону взгляда камеры.
    internal static class Fly
    {
        public static bool Active { get; private set; }

        // Время, до которого игнорируем урон от падения после выхода из полёта.
        public static float NoFallDamageUntil;

        // До этого времени полёт не включается снова (идёт сохранение или переход в другую локацию).
        private static float _holdUntil;

        // Перед сохранением и переходом в другую локацию возвращаем обычного игрока: с выключенным
        // контроллером игра не может сохраниться и пишет «на диске недостаточно места».
        public static void Pause(float seconds)
        {
            _holdUntil = Mathf.Max(_holdUntil, Time.unscaledTime + seconds);
            if (!Active) return;
            MelonLoader.MelonLogger.Msg($"Полёт: временно выключен на {seconds:0} с (сохранение или переход).");
            Disable();
        }

        private static vp_FPSController _controller;

        public static void Enable()
        {
            var player = GameManager.GetVpFPSPlayer();
            if (player == null) return;
            _controller = player.Controller;
            if (_controller == null) return;

            _controller.Stop();
            _controller.enabled = false;
            if (_controller.m_Controller != null) _controller.m_Controller.enabled = false;
            Active = true;
        }

        public static void Disable()
        {
            if (!Active) return;
            Active = false;
            NoFallDamageUntil = Time.unscaledTime + 5f;
            if (_controller == null) return;

            try
            {
                var t = GameManager.GetPlayerTransform();
                if (_controller.m_Controller != null) _controller.m_Controller.enabled = true;
                _controller.enabled = true;
                if (t != null) _controller.SetPosition(t.position);
                _controller.m_FallSpeed = 0f;
                _controller.m_FallDistance = 0f;
                _controller.Stop();
            }
            finally
            {
                _controller = null;
            }
        }

        public static void Tick()
        {
            bool want = State.On("fly") && Time.unscaledTime >= _holdUntil;
            if (want && !Active) Enable();
            if (!want && Active) Disable();
            if (!Active) return;

            var t = GameManager.GetPlayerTransform();
            var cam = GameManager.GetMainCamera();
            if (t == null || cam == null)
            {
                State.Set("fly", 0f);
                Disable();
                return;
            }

            var c = cam.transform;
            Vector3 dir = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) dir += c.forward;
            if (Input.GetKey(KeyCode.S)) dir -= c.forward;
            if (Input.GetKey(KeyCode.D)) dir += c.right;
            if (Input.GetKey(KeyCode.A)) dir -= c.right;
            if (Input.GetKey(KeyCode.Space)) dir += Vector3.up;
            if (Input.GetKey(KeyCode.LeftControl)) dir -= Vector3.up;
            if (dir == Vector3.zero) return;

            float speed = State.Get("flyspeed");
            if (Input.GetKey(KeyCode.LeftShift)) speed *= 4f;
            t.position += dir.normalized * speed * Time.deltaTime;
        }
    }
}
