"""
The Planner agent's tools. Rules that keep the agent safe:
  * the list is a fixed allow-list: nothing outside it can be called, whatever a model asks for
  * every tool is read-only and works only on the facts already in the request: no database, no network
  * every call is recorded (name, arguments, result, time) and returned to the backend for the audit trail
"""
from __future__ import annotations

import time
from datetime import datetime, timezone
from typing import Any, Callable

from .schemas import ToolCallRecord

# Lower number = more urgent kind of need.
NEED_PRIORITY = {"Medical": 1, "Shelter": 2, "Water": 3, "Food": 4, "Sanitation": 5, "Clothing": 6}


class ToolNotAllowed(Exception):
    pass


def triage_breakdown(score: int, urgency: str, reasons: list[str]) -> dict[str, Any]:
    """What the deterministic triage found. The agent must treat this as ground truth."""
    return {"score": score, "level": urgency, "pointsToCritical": max(0, 70 - score), "reasons": list(reasons)[:10]}


def need_priority_order(needs: list[dict[str, Any]]) -> dict[str, Any]:
    """The needs sorted from most to least life-critical."""
    ordered = sorted(needs, key=lambda n: (NEED_PRIORITY.get(n["category"], 99), n["category"]))
    return {"order": [{"category": n["category"], "quantity": n["quantity"], "rank": i + 1} for i, n in enumerate(ordered)]}


def vulnerability_summary(household: int, elderly: int, children: int, flooded: bool) -> dict[str, Any]:
    vulnerable = elderly + children
    flags = []
    if elderly:
        flags.append("elderly members need mobility support")
    if children:
        flags.append("children need safe shelter and food they can eat")
    if flooded:
        flags.append("home is flooded")
    return {"vulnerableCount": vulnerable, "vulnerableShare": round(vulnerable / household, 2), "flags": flags}


def deadline_pressure(needed_by: datetime | None, now: datetime) -> dict[str, Any]:
    if needed_by is None:
        return {"hoursRemaining": None, "band": "none"}
    if needed_by.tzinfo is None:
        needed_by = needed_by.replace(tzinfo=timezone.utc)
    hours = (needed_by - now).total_seconds() / 3600
    band = "overdue" if hours <= 0 else "under12" if hours <= 12 else "under24" if hours <= 24 else "under48" if hours <= 48 else "later"
    return {"hoursRemaining": round(hours, 1), "band": band}


def find_volunteers(candidates: list[dict[str, Any]], required_skills: list[str], target_district: str) -> dict[str, Any]:
    """Filters candidate volunteers based on required skills and district."""
    qualified = []
    for c in candidates:
        cand_skills = set(c.get("skills", []))
        has_req = all(s in cand_skills for s in required_skills)
        same_district = c.get("district", "").lower() == target_district.lower()
        qualified.append({
            "id": c["id"],
            "name": c.get("name", ""),
            "district": c.get("district", ""),
            "skills": c.get("skills", []),
            "satisfiesAllRequired": has_req,
            "sameDistrict": same_district,
            "isVerified": c.get("is_verified", False),
            "completedAssignments": c.get("completed_assignments", 0)
        })
    qualified.sort(key=lambda x: (x["satisfiesAllRequired"], x["sameDistrict"], x["completedAssignments"]), reverse=True)
    return {"candidates": qualified, "total": len(qualified)}


def volunteer_roster_summary(selected_ids: list[str], candidates: list[dict[str, Any]]) -> dict[str, Any]:
    """Summarizes coverage across selected volunteers."""
    cand_by_id = {c["id"]: c for c in candidates}
    skills_covered = set()
    districts = set()
    for vid in selected_ids:
        if vid in cand_by_id:
            skills_covered.update(cand_by_id[vid].get("skills", []))
            districts.add(cand_by_id[vid].get("district", ""))
    return {"selectedCount": len(selected_ids), "skillsCovered": sorted(list(skills_covered)), "districts": sorted(list(districts))}


def list_depots(depots: list[dict[str, Any]], target_district: str) -> dict[str, Any]:
    """Ranks available depots based on district proximity to the relief destination."""
    ranked = []
    for d in depots:
        dist = d.get("district", "").strip()
        same = dist.lower() == target_district.lower()
        items_count = len(d.get("items", []))
        total_avail = sum(i.get("quantity_available", 0) for i in d.get("items", []))
        ranked.append({
            "id": d["id"],
            "code": d.get("code", ""),
            "name": d.get("name", ""),
            "district": dist,
            "sameDistrict": same,
            "itemsCount": items_count,
            "totalAvailableUnits": total_avail,
            "items": d.get("items", [])
        })
    ranked.sort(key=lambda x: (x["sameDistrict"], x["totalAvailableUnits"]), reverse=True)
    return {"depots": ranked, "total": len(ranked)}


def check_stock(depots: list[dict[str, Any]], needed_category: str, needed_qty: int, target_district: str) -> dict[str, Any]:
    """Finds matching available stock for a specific category across candidate depots."""
    candidates = []
    total_found = 0

    for d in depots:
        dist = d.get("district", "").strip()
        same = dist.lower() == target_district.lower()
        for it in d.get("items", []):
            if it.get("category", "").lower() == needed_category.lower():
                avail = it.get("quantity_available", 0)
                if avail > 0:
                    total_found += avail
                    candidates.append({
                        "depotId": d["id"],
                        "depotName": d.get("name", ""),
                        "depotDistrict": dist,
                        "sameDistrict": same,
                        "itemId": it["id"],
                        "itemCode": it.get("code", ""),
                        "itemName": it.get("name", ""),
                        "category": it.get("category", ""),
                        "available": avail
                    })

    candidates.sort(key=lambda c: (c["sameDistrict"], c["available"]), reverse=True)
    shortage = max(0, needed_qty - total_found)
    return {
        "category": needed_category,
        "neededQuantity": needed_qty,
        "totalAvailable": total_found,
        "shortage": shortage,
        "isFullyCovered": shortage == 0,
        "candidateOptions": candidates
    }


def supply_allocation_summary(lines: list[dict[str, Any]], needs: list[dict[str, Any]]) -> dict[str, Any]:
    """Calculates coverage, total units allocated, and remaining shortages across needs."""
    allocated_by_cat: dict[str, int] = {}
    depots_used = set()
    total_units = 0

    for line in lines:
        cat = line.get("category", "")
        qty = line.get("quantity_allocated", 0)
        allocated_by_cat[cat] = allocated_by_cat.get(cat, 0) + qty
        total_units += qty
        if "depot_id" in line:
            depots_used.add(line["depot_id"])
        elif "depotId" in line:
            depots_used.add(line["depotId"])

    shortages = []
    for n in needs:
        cat = n.get("category", "")
        needed = n.get("quantity", 0)
        alloc = allocated_by_cat.get(cat, 0)
        if alloc < needed:
            shortages.append({"category": cat, "needed": needed, "allocated": alloc, "shortage": needed - alloc})

    return {
        "totalUnitsAllocated": total_units,
        "uniqueDepotsCount": len(depots_used),
        "shortagesCount": len(shortages),
        "shortages": shortages,
        "isAllCovered": len(shortages) == 0
    }


ALLOWED_TOOLS: dict[str, Callable[..., dict[str, Any]]] = {
    "triage_breakdown": triage_breakdown,
    "need_priority_order": need_priority_order,
    "vulnerability_summary": vulnerability_summary,
    "deadline_pressure": deadline_pressure,
    "find_volunteers": find_volunteers,
    "volunteer_roster_summary": volunteer_roster_summary,
    "list_depots": list_depots,
    "check_stock": check_stock,
    "supply_allocation_summary": supply_allocation_summary,
}



class ToolBox:
    """Runs allow-listed tools and keeps a record of every call."""

    def __init__(self, tools: dict[str, Callable[..., dict[str, Any]]] | None = None) -> None:
        self._tools = ALLOWED_TOOLS if tools is None else tools
        self.records: list[ToolCallRecord] = []

    def call(self, name: str, **arguments: Any) -> dict[str, Any]:
        if name not in self._tools:
            raise ToolNotAllowed(f"Tool '{name}' is not on the allow-list.")

        started = time.perf_counter()
        try:
            result = self._tools[name](**arguments)
            ok = True
        except Exception as exc:  # a failing tool is recorded, then re-raised to the graph
            self.records.append(ToolCallRecord(
                name=name, arguments=_jsonable(arguments), result={"error": str(exc)}, succeeded=False,
                duration_ms=int((time.perf_counter() - started) * 1000)))
            raise
        self.records.append(ToolCallRecord(
            name=name, arguments=_jsonable(arguments), result=result, succeeded=ok,
            duration_ms=int((time.perf_counter() - started) * 1000)))
        return result


def _jsonable(arguments: dict[str, Any]) -> dict[str, Any]:
    return {k: (v.isoformat() if isinstance(v, datetime) else v) for k, v in arguments.items()}
