# Component A: citizen mobile app (Flutter)

Owner: Gurusingha V S H (group 2026-PR-01). Backend: [A-relief-requests.md](A-relief-requests.md). Web console for coordinators: [A-web-console.md](A-web-console.md).

## Purpose
A family in a flood needs to say what they need and where they are, in a few taps, on a phone with a weak signal, and then know what is happening. Everything in the app serves those two jobs.

## Screens
| Screen | Job |
|---|---|
| Sign in / Create an account | Citizens register themselves (always the Citizen role) and sign in. Coordinators and admins are refused and no token is kept. |
| My requests | The person's own requests with status and needs. Pull to refresh. Empty state explains what to do. |
| Ask for help | Household (big − / + buttons), what is needed (tick, then how many), district, address, optional "Use my current location", when help is needed, notes. Emergency numbers 117 and 1990 are shown first. |
| Request details | Plain-language "what happens next", progress steps, what was asked for, household, **Change my request** (only while Submitted), **Cancel this request** (while it waits for a decision). |

## Decisions worth knowing
- **The phone never decides priority.** It sends facts only. The API computes the triage score and urgency (`TriageScorer`) and ignores any status, score or urgency sent by a client. A test proves the app never sends those fields.
- **Same rules on both sides.** `validateDraft` repeats the API's rules (household 1–50, elderly + children ≤ household, address 5–300 characters, 1–6 needs, quantity 1–1000, coordinates inside Sri Lanka, deadline within 30 days) so a person gets an instant, specific message. The API still checks everything. This is a convenience, not security. `tool/live_check.dart` confirms the real API agrees.
- **Location is optional.** If the person does not share it, or refuses permission, or location is off, the app uses the centre of the chosen district and says so. A shared position is sent with the request.
- **Nothing typed is lost.** When the server refuses a request or the signal drops, the form keeps everything and shows the reason at the top (the screen scrolls there).
- **Where the token lives.** In Android's encrypted storage (`flutter_secure_storage`, Keystore). It is cleared on sign out, when it expires, and when the API answers 401.
- **Staff are refused** on the phone (they get a message pointing to the web console) so a coordinator token is never stored on a phone app that is not built for them. Authorization is still enforced by the API on every call.
- **Network safety.** The release app allows plain http only for `10.0.2.2` and `localhost`. Any deployed API must use https.
- **Volunteers** can sign in; their screens belong to the volunteer component and are added there. For now they see a short welcome.

## Tests (68)
| File | What it proves |
|---|---|
| `models_and_logic_test.dart` | Districts (25, all inside the API's box), JSON reading, edit/cancel rules per status, status wording, every form rule at its boundary, what is sent to the API |
| `api_and_session_test.dart` | Bearer token, error mapping (field errors, 401, 403, no network, timeout, non-JSON), staff refused with no token kept, session restore and expiry, 401 signs out |
| `screens_test.dart` | Sign in and register flows, empty/loaded/failed list, detail buttons for every status, cancel dialog, form validation summary, location, server refusal keeps input, edit uses PUT with saved values |

Mutation spot checks (break the code, tests must fail). All seven were caught: cancel allowed while Approved; staff accepted; client sends a status; people-count rule off by one; 401 handler fires on a failed login; form submits invalid data; editing always allowed.

Bugs the tests found while building (kept as lessons for the viva): a text-field controller disposed while its dialog was still animating away; an error message left off-screen at the top after tapping Send at the bottom; a status label that overflowed on a narrow phone; an error that vanished when the screen refreshed after a refused cancel.

Also checked by hand: `tool/live_check.dart` against the real API (14 checks pass), and the screens rendered in a browser build against the real API (sign up, send request, details, cancel). The release APK builds and its manifest was inspected (package `lk.relieflink.relieflink_mobile`, label ReliefLink, permissions INTERNET and location). It was **not** run on a physical phone or emulator from this workspace, so install it on a real phone before the demo.

## Viva questions you should be able to answer
1. **Can a citizen choose their own urgency?** No. The app sends only facts. The API scores them and ignores any status, score or urgency from the client.
2. **If the app and the API disagree on a rule, which wins?** The API. The app's checks are only for fast feedback.
3. **What happens when the signal drops mid-request?** The call fails with a plain message and a retry path. The form keeps all input, so nothing is typed twice.
4. **Why is the location optional?** People may refuse permission or have no GPS fix. The district centre is a safe fallback and the screen says it is being used.
5. **Where is the sign-in token stored, and when is it removed?** Encrypted Android storage. Removed on sign out, expiry, and any 401.
6. **Why can a request be changed only while Submitted?** After that a coordinator may already be planning help for the original details. The API enforces it (409) and the app hides the button.
7. **How would you add Sinhala or Tamil?** Move the strings into Flutter's localisation files (`flutter_localizations`, ARB files) and add the locales. Not done yet; listed as future work.
8. **How do you know the app works against the real API, not just against mocks?** `tool/live_check.dart` uses the app's own `ApiClient` against a running API, including the rules that must be refused (outside Sri Lanka, cancel twice, another citizen's request).

## Screenshots
Rendered from the app's code in a browser build against the real API (not a phone screenshot): [sign in](../screenshots/mobile-sign-in.png), [ask for help](../screenshots/mobile-ask-for-help.png), [my requests](../screenshots/mobile-my-requests.png), [request details](../screenshots/mobile-request-details.png). Replace them with real phone screenshots before submission if you can.
