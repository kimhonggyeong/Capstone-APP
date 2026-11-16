"""Shared web/app progress, retry safety and concurrent update behavior."""
from concurrent.futures import ThreadPoolExecutor
import unittest
from unittest.mock import patch

from fastapi import FastAPI
from fastapi.testclient import TestClient

from services.scenario.data import store as store_module
from services.scenario.data.store import MemoryStore
from services.scenario.routes.mypage import router


class QuizProgressTests(unittest.TestCase):
    def setUp(self):
        self.store = MemoryStore()
        self.store_patch = patch.object(store_module, "_store", self.store)
        self.store_patch.start()
        app = FastAPI()
        app.include_router(router)
        self.client = TestClient(app)
        self.url = "/api/users/shared-account/quiz-progress"

    def tearDown(self):
        self.client.close()
        self.store_patch.stop()

    def post(self, body):
        response = self.client.post(self.url + "/events", json=body)
        self.assertEqual(response.status_code, 200, response.text)
        return response.json()["data"]

    def test_new_user_has_zero_progress(self):
        data = self.client.get(self.url + "?total_quiz_count=360").json()["data"]
        self.assertEqual(data["user_id"], "shared-account")
        self.assertEqual(data["answered_count"], 0)
        self.assertEqual(data["progress_percent"], 0)
        self.assertEqual(data["accuracy_percent"], 0)
        self.assertEqual(data["completed_sessions"], 0)

    def test_web_and_app_answers_complete_one_per_topic(self):
        self.post({"quiz_id": "quiz_028", "is_correct": True, "event_id": "app-definition"})
        self.post({"quiz_id": "quiz_029", "is_correct": False})  # Existing web payload.
        data = self.post({"quiz_id": "quiz_030", "is_correct": True, "event_id": "app-scenario"})
        self.assertEqual(data["answered_quiz_ids"], ["quiz_028", "quiz_029", "quiz_030"])
        self.assertEqual(data["attempt_count"], 3)
        self.assertEqual(data["correct_attempt_count"], 2)
        self.assertEqual(data["accuracy_percent"], 67)
        self.assertEqual(data["progress_percent"], 1)
        self.assertEqual(data["completed_sessions"], 0)

    def test_replay_does_not_count_retry_but_new_attempt_does(self):
        event = {"quiz_id": "quiz_028", "is_correct": True, "event_id": "same-request"}
        original = self.post(event)
        self.assertEqual(self.post(event), original)
        replay = self.post({**event, "event_id": "new-answer"})
        self.assertEqual(replay["answered_count"], 1)
        self.assertEqual(replay["attempt_count"], 2)
        self.assertEqual(replay["correct_attempt_count"], 2)

    def test_legacy_web_payload_without_event_id_still_records_each_attempt(self):
        self.post({"quiz_id": "quiz_029", "is_correct": False, "session_completed": False})
        data = self.post({"quiz_id": "quiz_029", "is_correct": True, "session_completed": False})
        self.assertEqual(data["answered_count"], 1)
        self.assertEqual(data["attempt_count"], 2)
        self.assertEqual(data["accuracy_percent"], 50)

    def test_session_completion_is_separate_and_retry_safe(self):
        event = {"quiz_id": None, "is_correct": None, "session_completed": True, "event_id": "end"}
        self.post(event)
        data = self.post(event)
        self.assertEqual(data["completed_sessions"], 1)
        self.assertEqual(data["attempt_count"], 0)
        self.assertEqual(data["answered_count"], 0)

    def test_preserves_existing_web_records_and_isolates_accounts(self):
        self.store.insert_one("quiz_progress", {
            "user_id": "shared-account", "answered_quiz_ids": ["quiz_028"],
            "attempt_count": 8, "correct_attempt_count": 6, "completed_sessions": 2,
            "other_legacy_field": "keep",
        })
        data = self.post({"quiz_id": "quiz_029", "is_correct": True, "event_id": "append"})
        self.assertEqual(data["attempt_count"], 9)
        self.assertEqual(data["correct_attempt_count"], 7)
        self.assertEqual(data["completed_sessions"], 2)
        self.assertEqual(self.store.find_one("quiz_progress", {"user_id": "shared-account"})["other_legacy_field"], "keep")
        other = self.client.get("/api/users/other-account/quiz-progress").json()["data"]
        self.assertEqual(other["answered_count"], 0)

    def test_invalid_or_empty_events_rejected(self):
        for body in ({}, {"quiz_id": "quiz_028"}, {"quiz_id": "", "is_correct": True},
                     {"quiz_id": " ", "is_correct": False}):
            self.assertEqual(self.client.post(self.url + "/events", json=body).status_code, 422)

    def test_progress_denominator_zero_and_negative(self):
        self.post({"quiz_id": "quiz_028", "is_correct": True})
        data = self.client.get(self.url + "?total_quiz_count=0").json()["data"]
        self.assertEqual(data["progress_percent"], 0)
        self.assertEqual(self.client.get(self.url + "?total_quiz_count=-1").status_code, 422)

    def test_concurrent_devices_and_retries_do_not_lose_counts(self):
        def record(number):
            return self.store.record_quiz_progress_event(
                "concurrent", quiz_id=f"quiz_{number:03}", is_correct=number % 2 == 0,
                session_completed=False, event_id=f"event-{number}", updated_at="2026-11-15T00:00:00Z",
            )
        # Send every event twice, in parallel, as if both a request and its retry arrived.
        with ThreadPoolExecutor(max_workers=8) as pool:
            list(pool.map(record, list(range(1, 61)) * 2))
        data = self.store.find_one("quiz_progress", {"user_id": "concurrent"})
        self.assertEqual(data["attempt_count"], 60)
        self.assertEqual(data["correct_attempt_count"], 30)
        self.assertEqual(len(data["answered_quiz_ids"]), 60)


if __name__ == "__main__":
    unittest.main()
