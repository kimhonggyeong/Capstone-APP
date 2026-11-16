from typing import Literal
from pydantic import BaseModel, Field

FactorDirection = Literal["긍정", "부정"]
Judgment = Literal["매수", "매도", "관망"]


class Factor(BaseModel):
    # 기존 Unity 및 이전 이력의 직접/간접 요인 표기를 유지한다.
    type: Literal["직접", "간접"] = "직접"
    direction: FactorDirection | None = None  # 과거(마이그레이션 전) 이력 문서엔 없을 수 있어 optional
    factor: str
    weight: float = Field(ge=0, le=100)


class JudgmentResponse(BaseModel):
    symbol: str
    judge: Judgment
    confidence: float = Field(ge=0, le=100)
    probabilities: dict[Judgment, float]
    summary: str
    factors: list[Factor]
    computed_at: str

    @classmethod
    def from_document(cls, doc: dict):
        return cls(
            symbol=doc["symbol"], judge=doc["judge"], confidence=doc["confidence"],
            probabilities=doc.get("probabilities") or empty_probabilities(),
            summary=doc["reason"], factors=[Factor(**f) for f in doc["factors"]],
            computed_at=doc["time"],
        )


class HistoryEntry(BaseModel):
    time: str
    judge: Judgment
    probabilities: dict[Judgment, float]
    reason: str
    changed: bool
    confidence: float = Field(default=0, ge=0, le=100)


class CompareRequest(BaseModel):
    symbol: str
    user_judge: Judgment


class CompareResponse(BaseModel):
    user_judge: Judgment
    ai_judge: Judgment
    ai_probabilities: dict[Judgment, float]
    ai_confidence: float = Field(ge=0, le=100)
    explanation: str
    highlighted_factors: list[str]


class LegacyCompareResponse(BaseModel):
    user_judge: Judgment
    ai_judge: Judgment
    ai_probabilities: dict[Judgment, float]
    ai_confidence: float = Field(ge=0, le=100)
    explanation: str
    highlighted_factors: dict[str, list[str]]


def empty_probabilities() -> dict:
    # 과거 confidence는 새 확률과 다르므로 확률을 임의로 역산하지 않는다.
    return {"매수": 0.0, "매도": 0.0, "관망": 0.0}
