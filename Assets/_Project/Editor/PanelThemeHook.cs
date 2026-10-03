using UnityEditor;

namespace SpaceShip.EditorTools
{
    [InitializeOnLoad]
    static class PanelThemeHook
    {
        const string PanelPath = "Assets/_Project/UI/PanelSettings.asset";

        static PanelThemeHook()
        {
            EditorApplication.delayCall += () =>
            {
                var panel = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(PanelPath);
                UiFonts.EnsureTheme(panel);
            };
        }
    }
}
