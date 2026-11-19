"""사용자별 시나리오 평가와 누적 투자 습관 API."""
from __future__ import annotations

from datetime import datetime, timezone

from fastapi import APIRouter, Query
from pydantic import BaseModel, Field, model_validator

from services.scenario.data.app_repository import AppRepository, NotFoundError
from services.scenario.routes.common import handled_error, ok

from services.scenario.play.session_service import ScenarioSessionService

router = APIRouter(prefix="/api/users", tags=["mypage"])


class QuizProgressEventIn(BaseModel):
    quiz_id: str | None = Field(default=None, min_length=1, max_length=128)
    is_correct: bool | None = None
    session_completed: bool = False
    # Older web clients omit this. App clients retain it when retrying.
    event_id: str | None = Field(default=None, min_length=1, max_length=128)

    @model_validator(mode="after")
    def validate_event(self):
        if self.quiz_id is not None and not self.quiz_id.strip():
            raise ValueError("문제 ID가 비어 있습니다.")
        if self.quiz_id and self.is_correct is None:
            raise ValueError("문제 풀이의 정답 여부가 필요합니다.")
        if not self.quiz_id and not self.session_completed:
            raise ValueError("문제 풀이 또는 세션 완료 정보가 필요합니다.")
        return self


def _quiz_progress_summary(value: dict | None, user_id: str, total: int) -> dict:
    raw = value or {}
    answered = sorted({str(item) for item in raw.get("answered_quiz_ids", []) if item})
    attempts = max(0, int(raw.get("attempt_count") or 0))
    correct = min(attempts, max(0, int(raw.get("correct_attempt_count") or 0)))
    return {
        "user_id": user_id,
        "answered_quiz_ids": answered,
        "answered_count": len(answered),
        "attempt_count": attempts,
        "correct_attempt_count": correct,
        "completed_sessions": max(0, int(raw.get("completed_sessions") or 0)),
        "total_quiz_count": total,
        "progress_percent": min(100, round(len(answered) / total * 100)) if total else 0,
        "accuracy_percent": round(correct / attempts * 100) if attempts else 0,
        "updated_at": raw.get("updated_at"),
    }


@router.get("/{user_id}/quiz-progress")
def get_quiz_progress(user_id: str, total_quiz_count: int = Query(default=360, ge=0)):
    try:
        value = AppRepository().get_quiz_progress(user_id)
        return ok(_quiz_progress_summary(value, user_id, total_quiz_count))
    except Exception as exc:
        return handled_error(exc)


@router.post("/{user_id}/quiz-progress/events")
def record_quiz_progress_event(user_id: str, body: QuizProgressEventIn):
    try:
        value = AppRepository().record_quiz_progress_event(
            user_id,
            quiz_id=body.quiz_id,
            is_correct=body.is_correct,
            session_completed=body.session_completed,
            event_id=body.event_id,
            updated_at=datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        )
        return ok(_quiz_progress_summary(value, user_id, 360))
    except Exception as exc:
        return handled_error(exc)


@router.get("/{user_id}/evaluations")
def list_evaluations(user_id: str):
    try:
        repository = AppRepository()
        values = repository.list_user_evaluations(user_id)
        summaries = [
            {
                "evaluation_id": item["evaluation_id"],
                "session_id": item["session_id"],
                "scenario_id": item["scenario_id"],
                "scenario_version": item["scenario_version"],
                "completed_at": item["completed_at"],
                "overall_score": item.get("decision_evaluation", {}).get("overall_score"),
                "cumulative_return_pct": item.get("portfolio_analysis", {}).get("cumulative_return_pct"),
                "summary": item.get("feedback", {}).get("summary", ""),
                "repeated_patterns": [
                    pattern["label"]
                    for pattern in item.get("behavior_patterns", [])
                    if pattern.get("classification") == "REPEATED_PATTERN"
                ],
            }
            for item in values
        ]
        return ok(summaries)
    except Exception as exc:
        return handled_error(exc)


@router.get("/{user_id}/evaluations/{evaluation_id}")
def get_evaluation(user_id: str, evaluation_id: str):
    try:
        value = AppRepository().get_scenario_evaluation(evaluation_id)
        if value.get("user_id") != user_id:
            raise NotFoundError("종합평가를 찾을 수 없습니다.")
        return ok(value)
    except Exception as exc:
        return handled_error(exc)

@router.get("/{user_id}/sessions")
def list_user_sessions(user_id: str):
    try:
        values = (
            ScenarioSessionService()
            .list_user_sessions(user_id)
        )

        return ok(values)

    except Exception as exc:
        return handled_error(exc)

@router.get("/{user_id}/behavior-profile")
def get_behavior_profile(user_id: str):
    try:
        value = AppRepository().get_user_profile(user_id)
        if value is None:
            value = {
                "schema_version": 1,
                "user_id": user_id,
                "completed_scenario_count": 0,
                "dimension_averages": {},
                "pattern_statistics": [],
                "portfolio_tendencies": {},
                "updated_at": None,
            }
        return ok(value)
    except Exception as exc:
        return handled_error(exc)

