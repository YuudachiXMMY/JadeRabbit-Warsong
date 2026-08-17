// VERTICAL SLICE — NOT FOR PRODUCTION (reference only; never refactor into Assets/Scripts)
// Validation Question: 踩拍苗刀战斗是否「手感即奖励」而非「附加摩擦」（R-FEEL-01 / M0 手感判据）
// Date: 2026-08-16
//
// 一键场景生成器：程序化装配 MiaodaoRhythmSlice 试玩场景。所有运行时组件在各自 Awake() 里
// 对空引用做 FindFirstObjectByType 兜底解析，故本生成器无需手工接线（设计目标：最小化 Editor 手工）。
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using JadeRabbit.Slice;

namespace JadeRabbit.Slice.EditorTools
{
    public static class SliceSceneBuilder
    {
        private const string SceneDir = "Assets/_VerticalSlice/MiaodaoRhythmSlice";
        private const string ScenePath = SceneDir + "/MiaodaoRhythmSlice.unity";

        [MenuItem("VerticalSlice/Build Miaodao Rhythm Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildFloor();
            BuildSystems();
            BuildPlayer();
            BuildEnemy();
            BuildCamera();
            BuildSun();

            System.IO.Directory.CreateDirectory(SceneDir);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log("[SliceSceneBuilder] MiaodaoRhythmSlice 场景已生成于 " + ScenePath + "。按 Play 试玩。");
        }

        [MenuItem("VerticalSlice/Open Slice Scene")]
        public static void OpenScene()
        {
            if (System.IO.File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
            }
            else
            {
                Debug.LogWarning("[SliceSceneBuilder] 场景不存在，请先运行 VerticalSlice/Build Miaodao Rhythm Scene。");
            }
        }

        private static void BuildFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(3f, 1f, 3f);
        }

        private static void BuildSystems()
        {
            var systems = new GameObject("SliceSystems");
            systems.AddComponent<SliceMetronome>();
            systems.AddComponent<RhythmJudge>();
            systems.AddComponent<ComboController>();
            systems.AddComponent<PerfectKinetics>();
            systems.AddComponent<LatencyCalibration>();
            systems.AddComponent<RhythmHUD>();
            systems.AddComponent<SliceRunManager>();
        }

        private static void BuildPlayer()
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 1f, -3f);
            ApplyColor(player, new Color(0.2f, 0.45f, 0.95f));

            player.AddComponent<PlayerController>();
            player.AddComponent<SliceInput>();
            player.AddComponent<MiaodaoSwing>();
        }

        private static void BuildEnemy()
        {
            var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = "Enemy";
            enemy.transform.position = new Vector3(0f, 1f, 3f);
            ApplyColor(enemy, new Color(0.9f, 0.2f, 0.2f));

            enemy.AddComponent<BeatEnemy>();
        }

        private static void BuildCamera()
        {
            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 12f, -7f);
            camGo.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraRig>();
        }

        private static void BuildSun()
        {
            var sunGo = new GameObject("Sun");
            var light = sunGo.AddComponent<Light>();
            light.type = LightType.Directional;
            sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void ApplyColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return;

            var mat = new Material(shader) { color = color };
            renderer.sharedMaterial = mat;
        }
    }
}
