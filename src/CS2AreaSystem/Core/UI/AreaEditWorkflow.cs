using System;
using Unity.Entities;
namespace CS2AreaSystem.Core.UI
{
    public static class AreaEditWorkflow
    {
        public static void Start(Entity area, Action<Entity> setRecreation,
            Action selectPrefab, Action selectEditMode, Action activateTool, Action<Entity> focus)
        {
            // Recreate enters native draw mode and returns to ObjectToolSystem on completion.
            // Ordinary boundary editing must clear that state before activating the area tool.
            setRecreation(Entity.Null);
            selectPrefab();
            selectEditMode();
            activateTool();
            focus(area);
        }
    }
}
