using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class FinanceQuizProgressSnapshot
{
    public List<string> answered_quiz_ids = new List<string>();
    public int attempt_count;
    public int correct_attempt_count;
    public int completed_sessions;
    public string updated_at;

    public float ProgressPercent(int totalQuizCount)
    {
        int count = new HashSet<string>(answered_quiz_ids ?? new List<string>()).Count;
        return totalQuizCount > 0 ? Math.Min(100f, count * 100f / totalQuizCount) : 0f;
    }
}

[Serializable]
public sealed class FinanceQuizProgressEvent
{
    public string event_id;
    public string quiz_id;
    public bool? is_correct;
    public bool session_completed;
}

/// <summary>서버 확인 기록과 아직 전송하지 못한 기록을 함께 보관합니다.</summary>
[Serializable]
public sealed class FinanceQuizProgressState
{
    public FinanceQuizProgressSnapshot confirmed = new FinanceQuizProgressSnapshot();
    public List<FinanceQuizProgressEvent> pending = new List<FinanceQuizProgressEvent>();

    public void Normalize()
    {
        if (confirmed == null) confirmed = new FinanceQuizProgressSnapshot();
        if (confirmed.answered_quiz_ids == null) confirmed.answered_quiz_ids = new List<string>();
        confirmed.answered_quiz_ids = confirmed.answered_quiz_ids
            .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        confirmed.attempt_count = Math.Max(0, confirmed.attempt_count);
        confirmed.correct_attempt_count = Math.Max(0,
            Math.Min(confirmed.attempt_count, confirmed.correct_attempt_count));
        confirmed.completed_sessions = Math.Max(0, confirmed.completed_sessions);
        if (pending == null) pending = new List<FinanceQuizProgressEvent>();
        pending = pending.Where(item => item != null && !string.IsNullOrEmpty(item.event_id)
            && ((!string.IsNullOrWhiteSpace(item.quiz_id) && item.is_correct.HasValue)
                || item.session_completed))
            .GroupBy(item => item.event_id).Select(group => group.First()).ToList();
    }

    public void QueueAnswer(string quizId, bool isCorrect)
    {
        pending.Add(new FinanceQuizProgressEvent
        {
            event_id = Guid.NewGuid().ToString("N"),
            quiz_id = quizId,
            is_correct = isCorrect,
        });
    }

    public void QueueSessionCompleted()
    {
        pending.Add(new FinanceQuizProgressEvent
        {
            event_id = Guid.NewGuid().ToString("N"),
            session_completed = true,
        });
    }

    public void Accept(string eventId, FinanceQuizProgressSnapshot snapshot)
    {
        confirmed = snapshot;
        if (!string.IsNullOrEmpty(eventId)) pending.RemoveAll(item => item.event_id == eventId);
        Normalize();
    }

    public FinanceQuizProgressSnapshot Project()
    {
        var result = new FinanceQuizProgressSnapshot
        {
            answered_quiz_ids = new List<string>(confirmed.answered_quiz_ids),
            attempt_count = confirmed.attempt_count,
            correct_attempt_count = confirmed.correct_attempt_count,
            completed_sessions = confirmed.completed_sessions,
            updated_at = confirmed.updated_at,
        };
        var answered = new HashSet<string>(result.answered_quiz_ids);
        foreach (FinanceQuizProgressEvent item in pending)
        {
            if (!string.IsNullOrEmpty(item.quiz_id))
            {
                answered.Add(item.quiz_id);
                result.attempt_count++;
                if (item.is_correct == true) result.correct_attempt_count++;
            }
            if (item.session_completed) result.completed_sessions++;
        }
        result.answered_quiz_ids = answered.ToList();
        return result;
    }
}
