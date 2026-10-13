import { Link } from "react-router-dom";
import { api } from "../api/client";
import type { RequestStatus } from "../api/types";
import { EmptyState, ErrorBanner, Loading } from "../components/Feedback";
import { deadlineText, waitingText } from "../lib/format";
import { useLoad } from "../lib/useLoad";
import { statusLabel } from "../lib/workflow";

const CLOSED: RequestStatus[] = ["Fulfilled", "Rejected", "Cancelled"];
const URGENCY_ORDER = ["Critical", "High", "Medium", "Low"];

function Bars({ data, order, limit }: { data: Record<string, number>; order?: string[]; limit?: number }) {
  const entries = Object.entries(data)
    .sort((a, b) => (order ? order.indexOf(a[0]) - order.indexOf(b[0]) : b[1] - a[1]))
    .slice(0, limit);
  const max = Math.max(1, ...entries.map(([, n]) => n));
  if (entries.length === 0) return <p className="muted">Nothing to show yet.</p>;
  return (
    <ul className="bars">
      {entries.map(([name, n]) => (
        <li key={name}>
          <span className="bars-name">{statusLabel(name)}</span>
          <span className="bars-track"><span className={`bars-fill fill-${name.toLowerCase().replace(/\s/g, "")}`} style={{ width: `${(n / max) * 100}%` }} /></span>
          <span className="bars-n">{n}</span>
        </li>
      ))}
    </ul>
  );
}

export function Dashboard() {
  const summary = useLoad(() => api.summary(), []);
  const critical = useLoad(() => api.requests({ urgency: "Critical", sort: "urgency", pageSize: 30 }), []);

  const waiting = (critical.data?.items ?? []).filter((r) => !CLOSED.includes(r.status)).slice(0, 5);
  const s = summary.data;

  return (
    <>
      <header className="page-head">
        <h1>Overview</h1>
        <p className="muted">What needs a coordinator right now.</p>
      </header>

      <ErrorBanner error={summary.error ?? critical.error} onRetry={() => { summary.reload(); critical.reload(); }} />

      {summary.loading && !s ? <Loading /> : s && (
        <section className="figures" aria-label="Totals">
          <div className="figure figure-critical">
            <p className="figure-n">{s.criticalOpen}</p>
            <p className="figure-label">Critical requests still open</p>
          </div>
          <div className="figure">
            <p className="figure-n">{s.open}</p>
            <p className="figure-label">Open requests</p>
          </div>
          <div className="figure">
            <p className="figure-n">{s.total}</p>
            <p className="figure-label">All requests received</p>
          </div>
        </section>
      )}

      <section className="panel" aria-labelledby="urgent-title">
        <div className="panel-head">
          <h2 id="urgent-title">Most urgent now</h2>
          <Link to="/requests">See the full queue</Link>
        </div>
        {critical.loading && !critical.data ? <Loading /> : waiting.length === 0 ? (
          <EmptyState title="No critical requests are open.">New critical requests will appear here as soon as they are submitted.</EmptyState>
        ) : (
          <ul className="queue">
            {waiting.map((r) => (
              <li key={r.id}>
                <Link className={`row row-${r.urgency.toLowerCase()}`} to={`/requests/${r.id}`}>
                  <span className="row-score"><span className="score">{r.triageScore}</span><span className="score-level">{r.urgency}</span></span>
                  <span className="row-main">
                    <span className="row-title">{r.district}</span>
                    <span className="row-sub">{r.addressText}</span>
                  </span>
                  <span className="row-side">
                    <span>{deadlineText(r.neededBy)}</span>
                    <span className="muted">Waiting {waitingText(r.createdAt)}</span>
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </section>

      {s && (
        <div className="split">
          <section className="panel" aria-labelledby="by-status"><h2 id="by-status">By status</h2><Bars data={s.byStatus} /></section>
          <section className="panel" aria-labelledby="by-urgency"><h2 id="by-urgency">By urgency</h2><Bars data={s.byUrgency} order={URGENCY_ORDER} /></section>
          <section className="panel" aria-labelledby="by-district"><h2 id="by-district">Busiest districts</h2><Bars data={s.byDistrict} limit={8} /></section>
        </div>
      )}
    </>
  );
}
