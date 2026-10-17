"""The Gemini library refuses any response schema that has a default value, and it refuses it BEFORE it makes a network call,
so a fake client in the other tests can never notice. This file runs the library's own schema conversion on every agent's draft
model. It was added after a live test found that all five agents failed this way."""
import pytest
from google import genai
from google.genai import types
from pydantic import BaseModel

# A component that is not in your repository yet is skipped, so this file works in any order of components.
MODULES = ["llm", "supply_llm", "match_llm", "logistics_llm", "shelter_llm"]


def _models(model: type[BaseModel], seen: set | None = None):
    seen = seen if seen is not None else set()
    if model in seen:
        return
    seen.add(model)
    yield model
    for field in model.model_fields.values():
        args = [field.annotation, *getattr(field.annotation, "__args__", ())]
        for a in args:
            if isinstance(a, type) and issubclass(a, BaseModel):
                yield from _models(a, seen)


@pytest.mark.parametrize("module", MODULES)
def test_every_draft_field_is_required(module):
    draft = pytest.importorskip(f"app.{module}")._Draft
    optional = [f"{m.__name__}.{name}" for m in _models(draft) for name, f in m.model_fields.items() if not f.is_required()]

    assert optional == [], f"Gemini rejects defaults in a response schema: {optional}"


@pytest.mark.parametrize("module", MODULES)
def test_the_gemini_library_accepts_the_schema(module):
    draft = pytest.importorskip(f"app.{module}")._Draft
    client = genai.Client(api_key="not-a-real-key")
    config = types.GenerateContentConfig(response_mime_type="application/json", response_schema=draft)

    try:
        client.models.generate_content(model="gemini-3.5-flash", contents="hi", config=config)
    except ValueError as exc:  # raised by the library's schema conversion, before any request is sent
        pytest.fail(f"The Gemini library rejected the {module} schema: {exc}")
    except Exception:
        pass  # anything else happened after the schema was accepted (no network or a bad key), which is fine here
