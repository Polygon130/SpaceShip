using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace SpaceShip
{
    public static class UiFonts
    {
        const string GlyphProbe = "舱室船壳装配结果质量推力供电热容散热产热未覆盖武器气阀半格损管范围覆盖通道导弹追踪吨米秒度拖到上再一次可以挪位置出格子就拆掉断电变暗仍然计入当前不能改布局放不下已没能原来的从左侧把按住松开货骨舰桥反应堆主推电磁炮装甲带敌方玩家静止满速加速度刹停倒车菜单星际商船面板必须还缺没有电能源之间走得到船员挡住路旋转右键朝向正方形条横竖对调开战返回操作键自动射击结构解体过热沿白口准才会线松手后航迹绕星轨道点进保持滑行档切减空每加最快淡弯跟画面外滚轮缩放撞了追击脱离占红踪平环火自由只公里策略看见或大画面格F自驾驶行三角标记清时小就成更惯性选框里双击镜头跟着随能量实全部战斗斗近优先源备攻0123456789./×：，（）";

        static FontAsset fontAsset;
        static bool loggedFailure;

        public static void Install(UIDocument document)
        {
            if (document == null || document.panelSettings == null)
                return;
#if UNITY_EDITOR
            EnsureTheme(document.panelSettings);
#endif
            FontAsset asset = GetAsset();
            if (asset == null)
                return;
            if (document.panelSettings.textSettings == null)
                document.panelSettings.textSettings = ScriptableObject.CreateInstance<PanelTextSettings>();
            document.panelSettings.textSettings.defaultFontAsset = asset;
        }

#if UNITY_EDITOR
        public static void EnsureTheme(PanelSettings panel)
        {
            if (panel == null || panel.themeStyleSheet != null)
                return;
            ThemeStyleSheet theme = LoadTheme();
            if (theme == null)
            {
                Debug.LogWarning("拼船面板没有载入运行时主题，播放时文字不会画出来。");
                return;
            }

            panel.themeStyleSheet = theme;
            UnityEditor.EditorUtility.SetDirty(panel);
            UnityEditor.AssetDatabase.SaveAssets();
        }

        static ThemeStyleSheet LoadTheme()
        {
            const string path = "Assets/_Project/UI/UnityDefaultRuntimeTheme.tss";
            var theme = UnityEditor.AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(path);
            if (theme != null)
                return theme;
            if (!System.IO.File.Exists(path))
                return null;
            UnityEditor.AssetDatabase.ImportAsset(path, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
            return UnityEditor.AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(path);
        }
#endif

        public static void Apply(VisualElement tree)
        {
            if (tree == null)
                return;
            bool runtime = tree.panel != null && tree.panel.contextType == ContextType.Player;
            FontAsset asset = runtime ? GetAsset() : null;
            FontDefinition definition = asset != null ? FontDefinition.FromSDFFont(asset) : default;
            if (tree is UnityEngine.UIElements.TextElement self)
                Style(self, runtime, asset, definition);
            tree.Query<UnityEngine.UIElements.TextElement>().ForEach(element => Style(element, runtime, asset, definition));
        }

        static void Style(UnityEngine.UIElements.TextElement element, bool runtime, FontAsset asset, FontDefinition definition)
        {
            if (runtime && asset != null)
                element.style.unityFontDefinition = definition;
            element.style.fontSize = 20;
            if (element.ClassListContains("text-bad"))
                element.style.color = new Color(0.86f, 0.22f, 0.18f);
            else
                element.style.color = element.ClassListContains("chip") || element.ClassListContains("drag-ghost") || element.ClassListContains("room-label")
                    ? new Color(0.08f, 0.09f, 0.1f)
                    : new Color(0.93f, 0.94f, 0.95f);
        }

        static FontAsset GetAsset()
        {
            if (fontAsset != null)
                return fontAsset;

            string[][] faces =
            {
                new[] { "SimHei", "Regular" },
                new[] { "Microsoft YaHei", "Regular" },
                new[] { "Microsoft YaHei UI", "Regular" },
                new[] { "DengXian", "Regular" },
                new[] { "NSimSun", "Regular" },
                new[] { "SimSun", "Regular" }
            };

            for (int i = 0; i < faces.Length; i++)
            {
                FontAsset created = CreateOsFont(faces[i][0], faces[i][1]);
                if (created == null)
                    continue;
                if (!EnsureShader(created))
                    continue;
                AddCharacters(created, GlyphProbe);
                if (created.characterTable == null || created.characterTable.Count == 0)
                    continue;
                fontAsset = created;
                return fontAsset;
            }

            if (!loggedFailure)
            {
                loggedFailure = true;
                Debug.LogWarning("播放时没能用系统黑体画出中文。");
            }

            return null;
        }

        static FontAsset CreateOsFont(string family, string style)
        {
            MethodInfo factory = null;
            MethodInfo[] methods = typeof(FontAsset).GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name != "CreateFontAsset")
                    continue;
                ParameterInfo[] parameters = methods[i].GetParameters();
                if (parameters.Length >= 2 && parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(string))
                {
                    factory = methods[i];
                    break;
                }
            }

            if (factory == null)
                return null;

            ParameterInfo[] argsInfo = factory.GetParameters();
            object[] args = new object[argsInfo.Length];
            args[0] = family;
            args[1] = style;
            for (int i = 2; i < argsInfo.Length; i++)
            {
                if (argsInfo[i].HasDefaultValue)
                    args[i] = argsInfo[i].DefaultValue;
                else if (argsInfo[i].ParameterType == typeof(int))
                    args[i] = 90;
            }

            try
            {
                return factory.Invoke(null, args) as FontAsset;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(exception.InnerException != null ? exception.InnerException.Message : exception.Message);
                return null;
            }
        }

        static void AddCharacters(FontAsset asset, string characters)
        {
            MethodInfo[] methods = typeof(FontAsset).GetMethods(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name != "TryAddCharacters")
                    continue;
                ParameterInfo[] parameters = methods[i].GetParameters();
                if (parameters.Length == 0 || parameters[0].ParameterType != typeof(string))
                    continue;
                object[] args = new object[parameters.Length];
                args[0] = characters;
                for (int p = 1; p < parameters.Length; p++)
                {
                    Type parameterType = parameters[p].ParameterType;
                    if (parameterType.IsByRef)
                        args[p] = null;
                    else if (parameterType == typeof(bool))
                        args[p] = false;
                    else if (parameters[p].HasDefaultValue)
                        args[p] = parameters[p].DefaultValue;
                }

                try
                {
                    methods[i].Invoke(asset, args);
                    return;
                }
                catch (Exception)
                {
                }
            }
        }

        static bool EnsureShader(FontAsset asset)
        {
            if (asset.material != null && asset.material.shader != null && asset.material.shader.name != "Hidden/InternalErrorShader")
                return true;

            Shader shader = FindTextShader();
            if (shader == null)
                return false;
            if (asset.material == null)
                asset.material = new Material(shader);
            else
                asset.material.shader = shader;
            if (asset.atlasTexture != null)
                asset.material.SetTexture("_MainTex", asset.atlasTexture);
            return true;
        }

        static Shader FindTextShader()
        {
            string[] names =
            {
                "TextCore/Mobile/Distance Field SSD",
                "Hidden/TextCore/Distance Field SSD",
                "TextCore/Distance Field SSD",
                "Text/SDF",
                "TextMeshPro/Mobile/Distance Field"
            };
            for (int i = 0; i < names.Length; i++)
            {
                Shader shader = Shader.Find(names[i]);
                if (shader != null)
                    return shader;
            }

            Shader[] loaded = Resources.FindObjectsOfTypeAll<Shader>();
            for (int i = 0; i < loaded.Length; i++)
            {
                if (loaded[i] != null && loaded[i].name.IndexOf("Distance Field", StringComparison.OrdinalIgnoreCase) >= 0)
                    return loaded[i];
            }

            return null;
        }
    }
}
