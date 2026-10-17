"""Settings come from environment variables only. Nothing secret is ever written in source code."""
from __future__ import annotations

import os
from dataclasses import dataclass


@dataclass(frozen=True)
class Settings:
    service_key: str
    llm_mode: str = "mock"            # "mock" (default, no key needed) or "gemini"
    gemini_api_key: str = ""
    gemini_model: str = "gemini-3.5-flash"
    llm_timeout_seconds: int = 20
    mock_fault: str = "none"          # testing/demo only: none | drop_need_first_attempt | lower_urgency

    @staticmethod
    def from_env() -> "Settings":
        return Settings(
            service_key=os.environ.get("AGENT_SERVICE_KEY", "dev-agent-service-key-0123456789"),
            llm_mode=os.environ.get("LLM_MODE", "mock").strip().lower(),
            gemini_api_key=os.environ.get("GEMINI_API_KEY", ""),
            gemini_model=os.environ.get("GEMINI_MODEL", "gemini-3.5-flash"),
            llm_timeout_seconds=int(os.environ.get("LLM_TIMEOUT_SECONDS", "20")),
            mock_fault=os.environ.get("MOCK_FAULT", "none").strip().lower(),
        )

    def validate(self) -> None:
        if len(self.service_key) < 16:
            raise RuntimeError(
                "AGENT_SERVICE_KEY is missing or shorter than 16 characters. "
                "Set it to a long random value and use the same value as Agent__ServiceKey in the API."
            )
        if self.llm_mode not in {"mock", "gemini"}:
            raise RuntimeError("LLM_MODE must be 'mock' or 'gemini'.")
        if self.llm_mode == "gemini" and not self.gemini_api_key:
            raise RuntimeError("LLM_MODE=gemini needs GEMINI_API_KEY to be set.")
        if self.mock_fault not in {"none", "drop_need_first_attempt", "lower_urgency"}:
            raise RuntimeError("MOCK_FAULT must be none, drop_need_first_attempt or lower_urgency.")
