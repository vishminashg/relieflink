import { useState, type FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { api, ApiError } from "../api/client";
import type { ReliefRequest, Urgency } from "../api/types";
import { StatusPill, UrgencyTag } from "../components/Badges";
import { ErrorBanner, Loading } from "../components/Feedback";
import { deadlineText, formatDateTime, waitingText } from "../lib/format";
import { useLoad } from "../lib/useLoad";
import { statusLabel } from "../lib/workflow";

const URGENCIES: Urgency[] = ["Critical", "High", "Medium", "Low"];
const CLOSED = ["Fulfilled", "Rejected", "Cancelled"];

export function RequestDetail() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const request = useLoad(() => api.request(id), [id]);
  const plans = useLoad(() => api.workflows({ reliefRequestId: id, pageSize: 20 }), [id]);

  const [urgency, setUrgency] = useState<Urgency>("High");
  const [reason, setReason] = useState("");
  const [cancelReason, setCancelReason] = useState("");
  const [actionError, setActionError] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  const r = request.data;

  async function run(action: () => Promise<ReliefRequest | unknown>, then?: (result: unknown) => void) {
    setBusy(true);
    setActionError(null);
    try {
      const result = await action();
      then?.(result);
    } catch (e) {
      setActionError(e instanceof ApiError ? e : new ApiError(0, "That did not work. Try again."));
    } finally {
      setBusy(false);
    }
  }

  const reasonOk = reason.trim().length >= 3;
  const cancelOk = cancelReason.trim().length >= 3;

  function override(e: FormEvent) {
    e.preventDefault();
    if (!r || !reasonOk) return;
    run(() => api.overrideUrgency(r.id, urgency, reason.trim()), (updated) => {
      request.setData(updated as ReliefRequest);
      setReason("");
    });
  }

  if (request.loading && !r) return <Loading />;
  if (!r) return <ErrorBanner error={request.error} onRetry={request.reload} />;

  const closed = CLOSED.includes(r.status);
  const hasLivePlan = (plans.data?.items ?? []).some((w) => !["Executed", "Rejected", "FailedSafe"].includes(w.status));

  return (
    <>
      <p className="crumbs"><Link to="/requests">Requests</Link> / {r.district}</p>
      <header className="page-head page-head-row">
        <div>
          <h1>{r.district}</h1>
          <p className="muted">{r.addressText}</p>
        </div>
        <div className="head-tags">
          <UrgencyTag urgency={r.urgency} overridden={r.urgencyOverridden} />
          <StatusPill status={r.status} />
        </div>
      </header>

      <ErrorBanner error={actionError} />

      <div className="detail-grid">
        <div className="stack">
          <section className="panel" aria-labelledby="needs-title">
            <h2 id="needs-title">What the household needs</h2>
            <ul className="needs">
              {r.items.map((i) => (
                <li key={i.id}>
                  <span className="need-qty">{i.quantity}</span>
                  <span><strong>{i.category}</strong>{i.description ? ` — ${i.description}` : ""}</span>
                </li>
              ))}
            </ul>
            {r.notes && (<><h3>Notes from the requester</h3><p className="notes">{r.notes}</p></>)}
          </section>

          <section className="panel" aria-labelledby="triage-title">
            <h2 id="triage-title">Why this score: {r.triageScore}</h2>
            <ul className="reasons">{r.triageReasons.map((t, i) => <li key={i}>{t}</li>)}</ul>
            {r.urgencyOverridden && (
              <p className="override-note">Urgency was set by hand to {r.urgency}. Reason: {r.urgencyOverrideReason}</p>
            )}
            {!closed && (
              <form className="inline-form" onSubmit={override}>
                <h3>Change the urgency</h3>
                <div className="form-row">
                  <label>New urgency
                    <select value={urgency} onChange={(e) => setUrgency(e.target.value as Urgency)}>
                      {URGENCIES.map((u) => <option key={u}>{u}</option>)}
                    </select>
                  </label>
                  <label className="grow">Reason for the change (at least 3 characters)
                    <input value={reason} onChange={(e) => setReason(e.target.value)} />
                  </label>
                  <button className="btn btn-primary" type="submit" disabled={busy || !reasonOk}>Save urgency</button>
                </div>
                {r.urgencyOverridden && (
                  <button type="button" className="btn btn-quiet" disabled={busy}
                    onClick={() => run(() => api.recomputeTriage(r.id), (updated) => request.setData(updated as ReliefRequest))}>
                    Go back to the automatic score
                  </button>
                )}
              </form>
            )}
          </section>
        </div>

        <aside className="stack">
          <section className="panel" aria-labelledby="facts-title">
            <h2 id="facts-title">Household</h2>
            <dl className="facts">
              <div><dt>People</dt><dd>{r.householdSize} ({r.elderlyCount} elderly, {r.childrenCount} children)</dd></div>
              <div><dt>Home flooded</dt><dd>{r.homeFlooded ? "Yes" : "No"}</dd></div>
              <div><dt>Needed by</dt><dd>{r.neededBy ? `${formatDateTime(r.neededBy)} (${deadlineText(r.neededBy)})` : "No deadline given"}</dd></div>
              <div><dt>Waiting</dt><dd>{waitingText(r.createdAt)}</dd></div>
              <div><dt>Contact</dt><dd>{r.requester ? `${r.requester.fullName}${r.requester.phone ? `, ${r.requester.phone}` : ""}` : "Not available"}</dd></div>
              <div><dt>Location</dt><dd>{r.latitude.toFixed(4)}, {r.longitude.toFixed(4)}</dd></div>
            </dl>
          </section>

          <section className="panel" aria-labelledby="ai-title">
            <h2 id="ai-title">AI relief plan</h2>
            {r.status === "Submitted" && !hasLivePlan && (
              <>
                <p>Ask the planning agent to draft a plan. You decide whether it goes ahead.</p>
                <button className="btn btn-primary" disabled={busy}
                  onClick={() => run(() => api.startWorkflow(r.id), (d) => navigate(`/workflows/${(d as { workflow: { id: string } }).workflow.id}`))}>
                  Draft a plan with AI
                </button>
              </>
            )}
            {r.status !== "Submitted" && !plans.data?.items.length && <p className="muted">No plan has been drafted for this request.</p>}
            {plans.data && plans.data.items.length > 0 && (
              <ul className="plain-list">
                {plans.data.items.map((w) => (
                  <li key={w.id}>
                    <Link to={`/workflows/${w.id}`}>{statusLabel(w.status)}</Link>
                    <span className="muted"> started {formatDateTime(w.createdAt)}</span>
                  </li>
                ))}
              </ul>
            )}
          </section>

          {!closed && (
            <section className="panel" aria-labelledby="cancel-title">
              <h2 id="cancel-title">Cancel this request</h2>
              <label className="stacked">Reason for cancelling (at least 3 characters)
                <input value={cancelReason} onChange={(e) => setCancelReason(e.target.value)} />
              </label>
              <button className="btn btn-danger" disabled={busy || !cancelOk}
                onClick={() => run(() => api.cancelRequest(r.id, cancelReason.trim()), (updated) => { request.setData(updated as ReliefRequest); plans.reload(); })}>
                Cancel request
              </button>
            </section>
          )}
          {r.cancellationReason && <p className="override-note">Cancelled: {r.cancellationReason}</p>}
        </aside>
      </div>
    </>
  );
}
