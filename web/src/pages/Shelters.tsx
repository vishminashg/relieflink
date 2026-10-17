import { useState } from "react";
import { api } from "../api/client";
import type { Shelter, ShelterInput } from "../api/types";
import { ErrorBanner, Loading } from "../components/Feedback";
import { DISTRICTS } from "../lib/format";
import { useLoad } from "../lib/useLoad";

const EMPTY: ShelterInput = {
  name: "", district: "Kalutara", address: "", latitude: 6.5854, longitude: 79.9607,
  capacity: 50, isActive: true, notes: "",
};

export function Shelters() {
  const [district, setDistrict] = useState("");
  const [search, setSearch] = useState("");
  const [editing, setEditing] = useState<Shelter | null>(null);
  const [form, setForm] = useState<ShelterInput>(EMPTY);
  const [message, setMessage] = useState<string | null>(null);
  const { data, error, loading, reload } = useLoad(
    () => api.shelters({ district: district || undefined, search: search || undefined, pageSize: 100 }),
    [district, search],
  );

  const update = (key: keyof ShelterInput, value: string | number | boolean) =>
    setForm((current) => ({ ...current, [key]: value }));

  const save = async (event: React.FormEvent) => {
    event.preventDefault();
    setMessage(null);
    try {
      if (editing) await api.updateShelter(editing.id, form);
      else await api.createShelter(form);
      setEditing(null);
      setForm(EMPTY);
      setMessage("Shelter saved.");
      reload();
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Could not save shelter.");
    }
  };

  const edit = (shelter: Shelter) => {
    setEditing(shelter);
    setForm({ name: shelter.name, district: shelter.district, address: shelter.address,
      latitude: shelter.latitude, longitude: shelter.longitude, capacity: shelter.capacity,
      isActive: shelter.isActive, notes: shelter.notes ?? "" });
  };

  return <>
    <header className="page-head">
      <h1>Shelters</h1>
      <p className="muted">Manage safe locations and monitor available capacity.</p>
    </header>
    <div className="filters">
      <label>District<select value={district} onChange={(e) => setDistrict(e.target.value)}>
        <option value="">All districts</option>{DISTRICTS.map((d) => <option key={d}>{d}</option>)}
      </select></label>
      <label className="filters-search">Search<input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Name or address" /></label>
    </div>
    <ErrorBanner error={error} onRetry={reload} />
    {message && <p className="override-note">{message}</p>}
    {loading && !data ? <Loading /> : <div className="shelter-grid">
      {(data?.items ?? []).map((shelter) => <article className="panel" key={shelter.id}>
        <div className="panel-head"><h2>{shelter.name}</h2><span className="pill">{shelter.isActive ? "Active" : "Inactive"}</span></div>
        <p>{shelter.address}, {shelter.district}</p>
        <p><strong>{shelter.available}</strong> spaces available of {shelter.capacity}</p>
        <button className="btn btn-quiet" onClick={() => edit(shelter)}>Edit</button>
      </article>)}
    </div>}
    <form className="panel shelter-form" onSubmit={save}>
      <h2>{editing ? "Edit shelter" : "Add shelter"}</h2>
      <div className="form-row">
        <label className="grow">Name<input required value={form.name} onChange={(e) => update("name", e.target.value)} /></label>
        <label>District<select value={form.district} onChange={(e) => update("district", e.target.value)}>{DISTRICTS.map((d) => <option key={d}>{d}</option>)}</select></label>
        <label>Capacity<input required type="number" min="1" value={form.capacity} onChange={(e) => update("capacity", Number(e.target.value))} /></label>
      </div>
      <label>Address<input required value={form.address} onChange={(e) => update("address", e.target.value)} /></label>
      <label className="check-inline"><input type="checkbox" checked={form.isActive} onChange={(e) => update("isActive", e.target.checked)} /> Available for new check-ins</label>
      <button className="btn btn-primary" type="submit">{editing ? "Save changes" : "Create shelter"}</button>
    </form>
  </>;
}
