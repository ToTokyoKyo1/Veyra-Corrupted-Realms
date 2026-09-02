namespace Veyra.Core
{
    public static class SceneNames
    {
        public const string MainMenu = "SCN_MainMenu";
        public const string World01Level01Tutorial = "SCN_W01_L01_Tutorial";
        public const string World01Level02ThornGuardian = "SCN_W01_L02_ThornGuardian";
        public const string World01Level03AshWatcher = "SCN_W01_L03_AshWatcher";
        public const string World01Level04ThreefoldAssault = "SCN_W01_L04_ThreefoldAssault";
        public const string BattlePrototype = "SCN_BattlePrototype";
    }

    public enum MainMenuEntryPoint
    {
        Main = 0,
        Heroes = 1,
        Levels = 2
    }

    /// <summary>
    /// One-shot in-memory navigation intent used while changing scenes. It is
    /// deliberately not saved and never changes campaign progress.
    /// </summary>
    public static class MainMenuEntryRequest
    {
        private static MainMenuEntryPoint pendingEntryPoint = MainMenuEntryPoint.Main;

        public static void Request(MainMenuEntryPoint entryPoint)
        {
            pendingEntryPoint = entryPoint;
        }

        public static MainMenuEntryPoint Consume()
        {
            MainMenuEntryPoint result = pendingEntryPoint;
            pendingEntryPoint = MainMenuEntryPoint.Main;
            return result;
        }
    }
}
