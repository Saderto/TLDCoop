// Язык трейнера: русский или английский. Файл компилируется и в окно, и в модуль игры.
// Окно хранит выбор в своих настройках и передаёт его модулю с каждым опросом (cmd "state", поле "lang").
namespace TrainerByEMPEROR.Shared
{
    public static class L
    {
        public static bool En;

        public static string Code => En ? "en" : "ru";

        public static string T(string ru, string en) => En ? en : ru;

        public static string F(string ru, string en, params object[] args) => string.Format(En ? en : ru, args);
    }
}
