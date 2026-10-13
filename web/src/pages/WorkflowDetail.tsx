import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api, ApiError } from "../api/client";
import type { Plan, Validation, WorkflowDetail as Detail } from "../api/types";
import { StatusPill } from "../components/Badges";
import { ErrorBanner, Loading } from "../components/Feedback";
import { JsonBlock } from "../components/JsonBlock";
import { WorkflowPath } from "../components/WorkflowPath";
import { formatDateTime } from "../lib/format";
import { useLoad } from "../lib/useLoad";
import { availableActions } from "../lib/workflow";

function PlanCard({ plan, title }: { plan: Plan; title: string }) {
  return (
    <section className="panel" aria-labelledby="plan-title">
      <h2 id="plan-title">{title}</h2>
      <p className="plan-summary">{plan.summary}</p>
      <p className="muted">Recommended urgency {plan.recommendedUrgency}. Aim to respond within {plan.targetResponseHours} h.</p>
      <table className="table">
        <caption className="sr-only">Items in the plan</caption>
        <thead><tr><th scope="col">Item</th><th scope="col">Quantity</th><th scope="col">What to do</th></tr></thead>
        <tbody>
          {plan.needs.map((n, i) => <tr key={i}><td>{n.category}</td><td>{n.quantity}</td><td>{n.action}</td></tr>)}
        </tbody>
      </table>
      {plan.rationale.length > 0 && (<><h3>Why the agent chose this</h3><ul className="reasons">{plan.rationale.map((t, i) => <li key={i}>{t}</li>)}</ul></>)}
      {plan.risks.length > 0 && (<><h3>Risks to watch</h3><ul className="reasons">{plan.risks.map((t, i) => <li key={i}>{t}</li>)}</ul></>)}
    </section>
  );
}

function Validations({ items }: { items: Validation[] }) {
  if (items.length === 0) return null;
  const attempts = [...new Set(items.map((v) => v.attempt))].sort((a, b) => a - b);
  return (
    <section className="panel" aria-labelledby="checks-title">
      <h2 id="checks-title">Automatic safety checks</h2>
      <p className="muted">Fixed rules, not the AI, check every plan before you see it.</p>
      {attempts.map((attempt) => {
        const rows = items.filter((v) => v.attempt === attempt);
        const failed = rows.filter((v) => !v.passed).length;
        return (
          <div key={attempt} className="attempt">
            <h3>Draft {attempt}: {failed === 0 ? "all checks passed" : `${failed} ${failed === 1 ? "check" : "checks"} failed`}</h3>
            <ul className="checks">
              {rows.map((v, i) => (
                <li key={i} className={v.passed ? "check-pass" : "check-fail"}>
                  <span className="check-mark" aria-hidden="true">{v.passed ? "✓" : "✕"}</span>
                  <span><strong>{v.checkName}</strong> <span className="sr-only">{v.passed ? "passed" : "failed"}</span>{v.detail ? ` — ${v.detail}` : ""}</span>
                </li>
              ))}
            </ul>
          </div>
        );
      })}
    </section>
  );
}

export function WorkflowDetail() {
  const { id = "" } = useParams();
  const { data, error, loading, reload, setData } = useLoad(() => api.workflow(id), [id]);
  const [comment, setComment] = useState("");
  const [rejectRequest, setRejectRequest] = useState(false);
  const [actionError, setActionError] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  if (loading && !data) return <Loading />;
  if (!data) return <ErrorBanner error={error} onRetry={reload} />;

  const { workflow: w } = data;
  const actions = availableActions(w.status);
  const commentOk = comment.trim().length >= 3;
  const shown = data.plan ?? data.proposal;

  async function act(call: () => Promise<Detail>, clear = true) {
    setBusy(true);
    setActionError(null);
    try {
      setData(await call());
      if (clear) { setComment(""); setRejectRequest(false); }
    } catch (e) {
      setActionError(e instanceof ApiError ? e : new ApiError(0, "That did not work. Try again."));
      reload();
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <p className="crumbs"><Link to="/workflows">AI plans</Link> / {w.objective}</p>
      <header className="page-head page-head-row">
        <div>
          <h1>{w.objective}</h1>
          <p className="muted">
            Started {formatDateTime(w.createdAt)}
            {w.reliefRequestId && <> · <Link to={`/requests/${w.reliefRequestId}`}>Open the request</Link></>}
          </p>
        </div>
        <div className="head-tags"><StatusPill status={w.status} /></div>
      </header>

      <WorkflowPath status={w.status} />
      {w.failureReason && <p className="override-note">Why it stopped: {w.failureReason}</p>}
      <ErrorBanner error={actionError} />

      {(actions.approve || actions.execute) && (
        <section className="panel decision" aria-labelledby="decide-title">
          <h2 id="decide-title">{actions.execute ? "Ready to carry out" : "Your decision"}</h2>
          {actions.execute ? (
            <>
              <p>This plan is approved. Executing it moves the request into progress.</p>
              <button className="btn btn-primary" disabled={busy} onClick={() => act(() => api.execute(w.id))}>Execute plan</button>
            </>
          ) : (
            <>
              <p>The plan passed every automatic check. Approve it, ask for a new draft, or reject it.</p>
              <label className="stacked">Comment (needed to revise or reject, at least 3 characters)
                <textarea rows={3} value={comment} onChange={(e) => setComment(e.target.value)} />
              </label>
              <label className="check-inline">
                <input type="checkbox" checked={rejectRequest} onChange={(e) => setRejectRequest(e.target.checked)} />
                When rejecting, also reject the relief request itself
              </label>
              <div className="button-row">
                <button className="btn btn-primary" disabled={busy} onClick={() => act(() => api.approve(w.id, comment.trim() || undefined))}>Approve plan</button>
                <button className="btn btn-quiet" disabled={busy || !commentOk} onClick={() => act(() => api.revise(w.id, comment.trim()))}>Ask for a new draft</button>
                <button className="btn btn-danger" disabled={busy || !commentOk} onClick={() => act(() => api.reject(w.id, comment.trim(), rejectRequest))}>Reject plan</button>
              </div>
            </>
          )}
        </section>
      )}

      {shown && <PlanCard plan={shown} title={data.plan ? "The plan" : "Proposed plan"} />}
      <Validations items={data.validations} />

      {data.decisions.length > 0 && (
        <section className="panel" aria-labelledby="decisions-title">
          <h2 id="decisions-title">Decisions</h2>
          <ul className="plain-list">
            {data.decisions.map((d, i) => (
              <li key={i}><strong>{d.decision}</strong> by {d.decidedByName ?? "a coordinator"} on {formatDateTime(d.createdAt)}{d.comment ? ` — “${d.comment}”` : ""}</li>
            ))}
          </ul>
        </section>
      )}

      <section className="panel" aria-labelledby="trail-title">
        <h2 id="trail-title">What the agent did</h2>
        <p className="muted">Every step and every data lookup is recorded so a plan can be audited later.</p>
        {data.steps.length === 0 ? <p className="muted">No steps recorded yet.</p> : (
          <ol className="trail">
            {data.steps.map((s) => (
              <li key={s.sequence}>
                <p><strong>{s.agentName}</strong> — {s.goal} <span className="muted">({s.status}{s.durationMs !== null ? `, ${s.durationMs} ms` : ""})</span></p>
                {s.error && <p className="override-note">{s.error}</p>}
                {s.toolCalls.length > 0 && (
                  <ul className="plain-list">
                    {s.toolCalls.map((t, i) => (
                      <li key={i}>
                        Looked up <code>{t.toolName}</code> {t.succeeded ? "" : "(failed)"}
                        <JsonBlock label="Details" value={{ arguments: t.arguments, result: t.result }} />
                      </li>
                    ))}
                  </ul>
                )}
              </li>
            ))}
          </ol>
        )}
      </section>
    </>
  );
}
