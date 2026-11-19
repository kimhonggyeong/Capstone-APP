using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

public static class ClientProgressTests
{
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        assertions++;
    }

    public static int Main()
    {
        var state = new FinanceQuizProgressState();
        state.QueueAnswer("quiz_028", true);
        string first = state.pending[0].event_id;
        Check(state.Project().answered_quiz_ids.Count == 1, "First answer must appear immediately.");
        Check(state.Project().ProgressPercent(360).ToString("F1", CultureInfo.InvariantCulture) == "0.3", "One answer must show 0.3%.");
        state.QueueAnswer("quiz_028", false);
        Check(state.Project().answered_quiz_ids.Count == 1, "Replays must not increase unique progress.");
        Check(state.Project().attempt_count == 2, "Replays must increase attempt count.");
        state.Accept(first, new FinanceQuizProgressSnapshot
        {
            answered_quiz_ids = new List<string> { "quiz_028" },
            attempt_count = 1, correct_attempt_count = 1,
        });
        Check(state.pending.Count == 1 && state.Project().attempt_count == 2, "Confirming one request must retain newer pending answers.");

        string retainedEvent = state.pending[0].event_id;
        state = JsonConvert.DeserializeObject<FinanceQuizProgressState>(JsonConvert.SerializeObject(state));
        state.Normalize();
        Check(state.pending[0].event_id == retainedEvent, "A restart must retain retry identity.");
        Check(state.Project().attempt_count == 2 && state.Project().correct_attempt_count == 1, "Restart must not drop or duplicate pending stats.");
        state.Accept(retainedEvent, new FinanceQuizProgressSnapshot
        {
            answered_quiz_ids = new List<string> { "quiz_028" },
            attempt_count = 2, correct_attempt_count = 1,
        });
        Check(state.pending.Count == 0 && state.Project().attempt_count == 2, "Acknowledgment must stop local double-counting.");

        state.QueueAnswer("quiz_031", false);
        state.Accept(null, new FinanceQuizProgressSnapshot
        {
            answered_quiz_ids = new List<string> { "quiz_028", "quiz_029", "quiz_030" },
            attempt_count = 3, correct_attempt_count = 2,
        });
        Check(state.Project().answered_quiz_ids.Count == 4 && state.pending.Count == 1, "Fetching web progress must retain a concurrently queued local answer.");
        state.QueueSessionCompleted();
        Check(state.Project().completed_sessions == 1 && state.Project().attempt_count == 4, "A completion must not count as another answer.");

        var otherAccount = new FinanceQuizProgressState();
        Check(otherAccount.Project().attempt_count == 0 && otherAccount.pending.Count == 0, "Account states must remain independent.");
        var emptyCache = JsonConvert.DeserializeObject<FinanceQuizProgressState>("{\"confirmed\":null,\"pending\":null}");
        emptyCache.Normalize();
        Check(emptyCache.Project().ProgressPercent(0) == 0, "Empty cache and zero total must be safe.");
        Console.WriteLine("Client progress state: " + assertions + " assertions passed.");
        Console.WriteLine("Sample app event: " + JsonConvert.SerializeObject(state.pending[0]));
        return 0;
    }
}
