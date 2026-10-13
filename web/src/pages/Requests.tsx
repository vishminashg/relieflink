import { useState } from "react";
import { Link } from "react-router-dom";
import { api } from "../api/client";
import type { RequestFilters, RequestStatus, Urgency } from "../api/types";
import { StatusPill } from "../components/Badges";
import { EmptyState, ErrorBanner, Loading } from "../components/Feedback";
import { Pager } from "../components/Pager";
import { DISTRICTS, deadlineText, waitingText } from "../lib/format";
import { useLoad } from "../lib/useLoad";
import { statusLabel } from "../lib/workflow";

const STATUSES: RequestStatus[] = ["Submitted", "UnderReview", "PlanReady", "Approved", "InProgress", "Fulfilled", "Rejected", "Cancelled"];
const URGENCIES: Urgency[] = ["Critical", "High", "Medium", "Low"];

export function Requests() {
  const [filters, setFilters] = useState<RequestFilters>({ sort: "urgency", page: 1, pageSize: 15 });
  const [searchText, setSearchText] = useState("");
  const { data, error, loading, reload } = useLoad(() => api.requests(filters), [JSON.stringify(filters)]);

  const set = (change: Partial<RequestFilters>) => setFilters((f) => ({ ...f, ...change, page: change.page ?? 1 }));

  return (
    <>
      <header className="page-head">
        <h1>Requests</h1>
        <p className="muted">The most urgent come first. Among equals, the one that has waited longest comes first.</p>
      </header>

      <form className="filters" role="search" onSubmit={(e) => { e.preventDefault(); set({ search: searchText.trim() }); }}>
        <label>Status
          <select value={filters.status ?? ""} onChange={(e) => set({ status: e.target.value as RequestStatus | "" })}>
            <option value="">Any status</option>
            {STATUSES.map((s) => <option key={s} value={s}>{statusLabel(s)}</option>)}
          </select>
        </label>
        <label>Urgency
          <select value={filters.urgency ?? ""} onChange={(e) => set({ urgency: e.target.value as Urgency | "" })}>
            <option value="">Any urgency</option>
            {URGENCIES.map((u) => <option key={u} value={u}>{u}</option>)}
          </select>
        </label>
        <label>District
          <select value={filters.district ?? ""} onChange={(e) => set({ district: e.target.value })}>
            <option value="">All districts</option>
            {DISTRICTS.map((d) => <option key={d} value={d}>{d}</option>)}
          </select>
        </label>
        <label>Order
          <select value={filters.sort} onChange={(e) => set({ sort: e.target.value as RequestFilters["sort"] })}>
            <option value="urgency">Most urgent first</option>
            <option value="newest">Newest first</option>
            <option value="oldest">Oldest first</option>
          </select>
        </label>
        <label className="filters-search">Search address or notes
          <input type="search" value={searchText} onChange={(e) => setSearchText(e.target.value)} placeholder="Road, town or keyword" />
        </label>
        <button className="btn btn-primary" type="submit">Search</button>
      </form>

      <ErrorBanner error={error} onRetry={reload} />

      {loading && !data ? <Loading /> : data && data.items.length === 0 ? (
        <EmptyState title="No requests match these filters.">Clear a filter or search for something else.</EmptyState>
      ) : data && (
        <>
          <p className="muted result-count">{data.totalCount} {data.totalCount === 1 ? "request" : "requests"}</p>
          <ul className="queue" aria-busy={loading}>
            {data.items.map((r) => (
              <li key={r.id}>
                <Link className={`row row-${r.urgency.toLowerCase()}`} to={`/requests/${r.id}`}>
                  <span className="row-score"><span className="score">{r.triageScore}</span><span className="score-level">{r.urgency}</span></span>
                  <span className="row-main">
                    <span className="row-title">{r.district}</span>
                    <span className="row-sub">{r.addressText}</span>
                    <span className="chips">{r.items.map((i) => <span className="chip" key={i.id}>{i.category} {i.quantity}</span>)}</span>
                  </span>
                  <span className="row-side">
                    <StatusPill status={r.status} />
                    <span>{deadlineText(r.neededBy)}</span>
                    <span className="muted">Waiting {waitingText(r.createdAt)}</span>
                  </span>
                </Link>
              </li>
            ))}
          </ul>
          <Pager page={data.page} totalPages={data.totalPages} onChange={(page) => set({ page })} />
        </>
      )}
    </>
  );
}
