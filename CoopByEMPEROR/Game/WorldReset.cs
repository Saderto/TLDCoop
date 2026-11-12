using MelonLoader;

namespace CoopByEMPEROR.Game
{
    // Мир сменился: хост загрузил другой мир (или снова тот же), друг просит мир хоста ещё раз.
    // Всё, что помнит прошлый мир (отложенные двери, ящики, события, ящики «пришли по сети», вагоны поезда,
    // таблица итогов, рецепты, испытание), забываем здесь, в одном месте, чтобы ничего из прошлого мира не легло поверх нового.
    public static class WorldReset
    {
        public static void Run(string why)
        {
            MelonLogger.Msg("[мир] " + why + ": забываю всё отложенное от прошлого мира");
            ItemSync.Reset();
            WorldObjects.Reset();
            SceneSnapshot.Reset();
            RadialSync.Reset();
            WorldEvents.Reset();
            ContainerLoot.Reset();
            TrainSync.ForgetWorld();
            IslandSync.ForgetWorld();
            Summary.ForgetWorld();
            SharedRecipes.Reset();      // флаг «рецепты уже слиты» и общие рецепты — от прошлого мира
            Challenges.ForgetWorld();   // хост: испытание и выполненные прошлого мира (друзьям — новое состояние)
        }
    }
}
