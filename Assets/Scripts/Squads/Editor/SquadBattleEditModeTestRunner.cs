#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class SquadBattleEditModeTestRunner
{
    private const string ResultPath = "Logs/SquadBattleIntegrationTestResults.xml";
    private const string RunRequestPath =
        "Assets/Scripts/Squads/Editor/SquadBattleTests.run-request";

    private static TestRunnerApi activeApi;
    private static ResultCallbacks activeCallbacks;

    [InitializeOnLoadMethod]
    private static void RunWhenRequested()
    {
        if (!File.Exists(RunRequestPath))
            return;

        EditorApplication.delayCall += () =>
        {
            AssetDatabase.DeleteAsset(RunRequestPath);
            RunAllEditModeTests();
        };
    }

    [MenuItem("Tools/Squads/Run All Edit Mode Tests %#&t")]
    public static void RunAllEditModeTests()
    {
        if (File.Exists(RunRequestPath))
            AssetDatabase.DeleteAsset(RunRequestPath);

        Directory.CreateDirectory("Logs");
        activeApi = ScriptableObject.CreateInstance<TestRunnerApi>();
        activeCallbacks = new ResultCallbacks();
        activeApi.RegisterCallbacks(activeCallbacks);
        activeApi.Execute(new ExecutionSettings(
            new Filter { testMode = TestMode.EditMode }));
    }

    private sealed class ResultCallbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun)
        {
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, ResultPath);
            Debug.Log(
                $"Squad edit-mode verification finished: " +
                $"passed={result.PassCount}, failed={result.FailCount}, skipped={result.SkipCount}. " +
                $"Results: {ResultPath}");
            activeApi = null;
            activeCallbacks = null;
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
        }
    }
}
#endif
