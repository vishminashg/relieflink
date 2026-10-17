import { useState } from "react";
import { Link } from "react-router-dom";
import { api } from "../api/client";
import type { VolunteerFilters, VolunteerSkillType } from "../api/types";
import { EmptyState, ErrorBanner, Loading } from "../components/Feedback";
import { Pager } from "../components/Pager";
import { DISTRICTS, VOLUNTEER_SKILLS, skillLabel } from "../lib/format";
import { useLoad } from "../lib/useLoad";

export function Volunteers() {
  const [filters, setFilters] = useState<VolunteerFilters>({
    sort: "createdAt_desc",
    page: 1,
    pageSize: 15,
  });
  const [searchText, setSearchText] = useState("");

  const { data, error, loading, reload } = useLoad(
    () => api.volunteers(filters),
    [JSON.stringify(filters)]
  );

  const { data: summary } = useLoad(() => api.volunteerSummary(), []);

  const set = (change: Partial<VolunteerFilters>) =>
    setFilters((f) => ({ ...f, ...change, page: change.page ?? 1 }));

  return (
    <>
      <header className="page-head">
        <div className="page-head-row">
          <div>
            <h1>Volunteers</h1>
            <p className="muted">
              Field roster, certified capabilities, and active deployment status across Sri Lanka.
            </p>
          </div>
          <div>
            <Link className="btn btn-primary" to="/volunteers/match">
              Match &amp; Dispatch
            </Link>
          </div>
        </div>
      </header>

      {summary && (
        <div className="figures" aria-label="Volunteer statistics">
          <div className="figure">
            <p className="figure-n">{summary.totalVolunteers}</p>
            <p className="figure-label">Total registered</p>
          </div>
          <div className="figure">
            <p className="figure-n">{summary.activeVolunteers}</p>
            <p className="figure-label">Active volunteers</p>
          </div>
          <div className="figure">
            <p className="figure-n">{summary.currentlyAssignedVolunteers}</p>
            <p className="figure-label">Deployed on mission</p>
          </div>
        </div>
      )}

      <form
        className="filters"
        role="search"
        onSubmit={(e) => {
          e.preventDefault();
          set({ search: searchText.trim() });
        }}
      >
        <label>
          District
          <select
            value={filters.district ?? ""}
            onChange={(e) => set({ district: e.target.value })}
          >
            <option value="">All districts</option>
            {DISTRICTS.map((d) => (
              <option key={d} value={d}>
                {d}
              </option>
            ))}
          </select>
        </label>

        <label>
          Skill
          <select
            value={filters.skill ?? ""}
            onChange={(e) => set({ skill: (e.target.value as VolunteerSkillType) || "" })}
          >
            <option value="">All skills</option>
            {VOLUNTEER_SKILLS.map((s) => (
              <option key={s} value={s}>
                {skillLabel(s)}
              </option>
            ))}
          </select>
        </label>

        <label>
          Status
          <select
            value={filters.isActive === undefined ? "" : String(filters.isActive)}
            onChange={(e) => {
              const val = e.target.value;
              set({ isActive: val === "" ? "" : val === "true" });
            }}
          >
            <option value="">All status</option>
            <option value="true">Active only</option>
            <option value="false">Inactive only</option>
          </select>
        </label>

        <label>
          Order
          <select
            value={filters.sort ?? "createdAt_desc"}
            onChange={(e) => set({ sort: e.target.value as VolunteerFilters["sort"] })}
          >
            <option value="createdAt_desc">Newest first</option>
            <option value="createdAt_asc">Oldest first</option>
            <option value="name_asc">Name (A-Z)</option>
            <option value="name_desc">Name (Z-A)</option>
            <option value="district_asc">District (A-Z)</option>
          </select>
        </label>

        <label className="filters-search">
          Search name, email, phone
          <input
            type="search"
            value={searchText}
            onChange={(e) => setSearchText(e.target.value)}
            placeholder="Volunteer name or contact"
          />
        </label>

        <button className="btn btn-primary" type="submit">
          Search
        </button>
      </form>

      <ErrorBanner error={error} onRetry={reload} />

      {loading && !data ? (
        <Loading />
      ) : data && data.items.length === 0 ? (
        <EmptyState title="No volunteers match these filters.">
          Try clearing filters or searching with a different keyword.
        </EmptyState>
      ) : (
        data && (
          <>
            <p className="muted result-count">
              {data.totalCount} {data.totalCount === 1 ? "volunteer" : "volunteers"}
            </p>
            <ul className="queue" aria-busy={loading}>
              {data.items.map((v) => (
                <li key={v.id}>
                  <Link className="row row-plain" to={`/volunteers/${v.id}`}>
                    <span className="row-score">
                      <span className="score">{v.skills.length}</span>
                      <span className="score-level">skills</span>
                    </span>
                    <span className="row-main">
                      <span className="row-title">
                        {v.user?.fullName ?? "Volunteer"} &bull; {v.district}
                      </span>
                      <span className="row-sub">
                        {v.user?.email}
                        {v.user?.phone ? ` \u2022 ${v.user.phone}` : ""}
                        {v.emergencyContactName ? ` \u2022 ICE: ${v.emergencyContactName} (${v.emergencyContactPhone})` : ""}
                      </span>
                      <span className="chips">
                        {v.skills.map((s) => (
                          <span className="chip" key={s.id}>
                            {skillLabel(s.skill)} ({s.proficiency})
                            {s.isVerified && <span title="Verified credential"> ✓</span>}
                          </span>
                        ))}
                      </span>
                    </span>
                    <span className="row-side">
                      <span
                        className={`pill ${
                          v.isActive ? "status-approved" : "status-rejected"
                        }`}
                      >
                        {v.isActive ? "Active" : "Inactive"}
                      </span>
                      <span>
                        {v.activeAssignmentsCount > 0
                          ? `${v.activeAssignmentsCount} active assignment${
                              v.activeAssignmentsCount > 1 ? "s" : ""
                            }`
                          : "Available now"}
                      </span>
                      <span className="muted">Max {v.maxHoursPerWeek}h/week</span>
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
            <Pager
              page={data.page}
              totalPages={data.totalPages}
              onChange={(page) => set({ page })}
            />
          </>
        )
      )}
    </>
  );
}
