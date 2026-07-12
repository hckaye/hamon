// E2E は1テストあたり多ウィジェット・多フレーム駆動で重い。アロケ計測の安定（スレッド別カウンタは並列でも別個だが、
// CPU 競合での実時間ブレやノイズを避ける）と決定論のため、テスト並列化を無効にする（Server.E2E.Tests と同方針）。
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
