#!/usr/bin/env python3
"""Compare read-only HrSchemaTool JSON metadata snapshots; never emits DDL/backfill."""

from __future__ import annotations

import argparse
import csv
import json
import math
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


COLUMN_FIELDS = (
    "dataType",
    "dataLengthBytes",
    "precision",
    "scale",
    "charLength",
    "charUsed",
    "nullable",
    "default",
    "hidden",
    "virtual",
)


def load_snapshot(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as stream:
        snapshot = json.load(stream)
    if not isinstance(snapshot, dict) or snapshot.get("SnapshotVersion") != 1:
        raise ValueError(f"{path} is not a supported HR schema snapshot (expected SnapshotVersion=1).")
    for section in ("Tables", "Columns", "Identities", "Constraints", "Indexes", "Triggers", "ForeignTriggers", "StaticDependencies"):
        if not isinstance(snapshot.get(section), list):
            raise ValueError(f"{path} is missing the {section} list.")
    if "TriggerSourceHashes" in snapshot and not isinstance(snapshot["TriggerSourceHashes"], list):
        raise ValueError(f"{path} has an invalid TriggerSourceHashes list.")
    for row in snapshot.get("TriggerSourceHashes", []):
        if not isinstance(row, dict) or not all(row.get(field) is not None for field in ("table", "name", "lineCount", "sha256")):
            raise ValueError(f"{path} contains an invalid trigger source hash record.")
    return snapshot


def key(value: Any) -> str:
    return "" if value is None else str(value).strip().upper()


def column_map(snapshot: dict[str, Any], label: str) -> dict[tuple[str, str], dict[str, Any]]:
    result: dict[tuple[str, str], dict[str, Any]] = {}
    for column in snapshot["Columns"]:
        if not isinstance(column, dict) or not column.get("table") or not column.get("column"):
            raise ValueError(f"{label} snapshot contains an invalid column record.")
        item_key = (key(column["table"]), key(column["column"]))
        if item_key in result:
            raise ValueError(f"{label} snapshot contains duplicate column {item_key[0]}.{item_key[1]}.")
        result[item_key] = column
    return result


def canonical(value: Any) -> str:
    return "" if value is None else str(value).strip()


def capture_time(snapshot: dict[str, Any]) -> datetime | None:
    raw_value = snapshot.get("CapturedAtUtc")
    if not isinstance(raw_value, str) or not raw_value.strip():
        return None
    try:
        parsed = datetime.fromisoformat(raw_value.strip().replace("Z", "+00:00"))
    except ValueError:
        return None
    if parsed.tzinfo is None:
        return None
    return parsed.astimezone(timezone.utc)


def identity_map(snapshot: dict[str, Any]) -> dict[tuple[str, str], dict[str, Any]]:
    return {
        (key(row.get("table")), key(row.get("column"))): row
        for row in snapshot.get("Identities", [])
        if row.get("table") and row.get("column")
    }


def trigger_source_coverage(snapshot: dict[str, Any]) -> tuple[set[tuple[str, str]], set[tuple[str, str]]]:
    triggers = {
        (key(row.get("table")), key(row.get("name")))
        for row in snapshot.get("Triggers", [])
        if row.get("table") and row.get("name")
    }
    hashed = {
        (key(row.get("table")), key(row.get("name")))
        for row in snapshot.get("TriggerSourceHashes", [])
        if row.get("table") and row.get("name")
    }
    return triggers - hashed, hashed - triggers


def grouped_signatures(
    rows: list[dict[str, Any]],
    group_fields: tuple[str, ...],
    signature_fields: tuple[str, ...],
    sequence_field: str | None = None,
    local_owner: str | None = None,
) -> Counter[tuple[Any, ...]]:
    grouped: dict[tuple[str, ...], list[dict[str, Any]]] = defaultdict(list)
    for row in rows:
        group = tuple(key(row.get(field)) for field in group_fields)
        grouped[group].append(row)

    result: Counter[tuple[Any, ...]] = Counter()
    for group, members in grouped.items():
        if sequence_field:
            def sequence(row: dict[str, Any]) -> int:
                try:
                    return int(row.get(sequence_field) or 0)
                except (TypeError, ValueError):
                    return 0

            members.sort(key=lambda row: (sequence(row), key(row.get("column"))))
        attributes = tuple(canonical(members[0].get(field)) for field in signature_fields)
        columns = tuple(
            (
                sequence(row) if sequence_field else 0,
                key(row.get("column")),
                (
                    "LOCAL" if local_owner is not None and key(row.get("referencedOwner")) == key(local_owner)
                    else key(row.get("referencedOwner"))
                ),
                key(row.get("referencedTable")),
                key(row.get("referencedColumn")),
                canonical(row.get("expression")),
            )
            for row in members
            if row.get("column") not in (None, "NULL", "")
        )
        result[(group, attributes, columns)] += 1
    return result


def counter_delta(expected: Counter[tuple[Any, ...]], actual: Counter[tuple[Any, ...]]) -> dict[str, list[dict[str, Any]]]:
    def as_rows(counter: Counter[tuple[Any, ...]]) -> list[dict[str, Any]]:
        return [{"signature": list(signature), "count": count} for signature, count in sorted(counter.items(), key=lambda item: repr(item[0]))]

    return {
        "missingFromTarget": as_rows(expected - actual),
        "extraInTarget": as_rows(actual - expected),
    }


def without_object_names(signatures: Counter[tuple[Any, ...]]) -> Counter[tuple[Any, ...]]:
    result: Counter[tuple[Any, ...]] = Counter()
    for (group, attributes, columns), count in signatures.items():
        result[(group[0], attributes, columns)] += count
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description="Compare source and target HR schema metadata snapshots; no database access or DDL.")
    parser.add_argument("--source", required=True, type=Path, help="HrSchemaTool source JSON snapshot")
    parser.add_argument("--target", required=True, type=Path, help="HrSchemaTool target JSON snapshot from an isolated schema")
    parser.add_argument("--output", required=True, type=Path, help="Path for the JSON comparison report")
    parser.add_argument("--matrix-output", type=Path, help="Optional CSV per-column review matrix; defaults to the JSON output path with .csv extension")
    parser.add_argument("--max-capture-skew-hours", type=float, default=24.0, help="Warn when snapshot capture times differ by more than this many hours (default: 24); this is a review hint, not an approval gate")
    args = parser.parse_args()

    source_path = args.source.resolve()
    target_path = args.target.resolve()
    output_path = args.output.resolve()
    matrix_path = (args.matrix_output or args.output.with_suffix(".csv")).resolve()
    if source_path == target_path:
        parser.error("--source and --target must refer to separate snapshot files.")
    if output_path in (source_path, target_path) or matrix_path in (source_path, target_path):
        parser.error("report outputs must not overwrite either input snapshot.")
    if output_path == matrix_path:
        parser.error("JSON report and CSV matrix must use separate output files.")
    if not math.isfinite(args.max_capture_skew_hours) or args.max_capture_skew_hours < 0:
        parser.error("--max-capture-skew-hours must be a finite, non-negative number.")

    source = load_snapshot(args.source)
    target = load_snapshot(args.target)
    source_capture = capture_time(source)
    target_capture = capture_time(target)
    warnings: list[str] = []
    capture_skew_seconds: float | None = None
    if source_capture is None or target_capture is None:
        warnings.append("One or both snapshots have no valid timezone-aware CapturedAtUtc; freshness cannot be assessed.")
    else:
        capture_skew_seconds = abs((source_capture - target_capture).total_seconds())
        if capture_skew_seconds > args.max_capture_skew_hours * 3600:
            warnings.append(
                f"Snapshot capture times differ by {capture_skew_seconds / 3600:.2f} hours, "
                f"exceeding the {args.max_capture_skew_hours:g}-hour review threshold; "
                "treat schema differences as time-sensitive until both schemas are reviewed at comparable times."
            )
    trigger_source_warnings: list[str] = []
    for label, snapshot in (("source", source), ("target", target)):
        missing_hashes, orphan_hashes = trigger_source_coverage(snapshot)
        if missing_hashes:
            trigger_source_warnings.append(
                f"{label} snapshot has {len(missing_hashes)} HR trigger(s) without source hashes; trigger-body comparison is incomplete."
            )
        if orphan_hashes:
            trigger_source_warnings.append(
                f"{label} snapshot has {len(orphan_hashes)} trigger source hash record(s) without a matching trigger metadata row."
            )
    source_columns = column_map(source, "source")
    target_columns = column_map(target, "target")
    source_identities = identity_map(source)
    target_identities = identity_map(target)
    source_keys, target_keys = set(source_columns), set(target_columns)
    shared_keys = source_keys & target_keys
    matrix_rows = []
    for table, column in sorted(source_keys | target_keys):
        source_column = source_columns.get((table, column), {})
        target_column = target_columns.get((table, column), {})
        source_identity = source_identities.get((table, column), {})
        target_identity = target_identities.get((table, column), {})
        source_present = bool(source_column)
        target_present = bool(target_column)
        source_non_nullable = key(source_column.get("nullable")) == "N"
        source_has_default = canonical(source_column.get("default")) not in ("", "NULL")
        if not source_present:
            change_kind = "SOURCE_MISSING"
            matches = ""
        elif not target_present:
            change_kind = "TARGET_MISSING"
            matches = ""
        else:
            matches = all(canonical(source_column.get(field)) == canonical(target_column.get(field)) for field in COLUMN_FIELDS)
            identity_matches = key(source_identity.get("generationType")) == key(target_identity.get("generationType"))
            change_kind = "MATCHED" if matches and identity_matches else "DEFINITION_DIFF"
            matches = "Y" if matches and identity_matches else "N"

        row = {
            "source_owner": source.get("Owner", ""),
            "target_owner": target.get("Owner", ""),
            "table": table,
            "column": column,
            "change_kind": change_kind,
            "source_present": "Y" if source_present else "N",
            "source_data_type": source_column.get("dataType", ""),
            "source_data_length_bytes": source_column.get("dataLengthBytes", ""),
            "source_precision": source_column.get("precision", ""),
            "source_scale": source_column.get("scale", ""),
            "source_char_length": source_column.get("charLength", ""),
            "source_char_used": source_column.get("charUsed", ""),
            "source_nullable": source_column.get("nullable", ""),
            "source_default": source_column.get("default", ""),
            "source_hidden": source_column.get("hidden", ""),
            "source_virtual": source_column.get("virtual", ""),
            "source_identity_generation": source_identity.get("generationType", ""),
            "source_identity_sequence": source_identity.get("sequenceName", ""),
            "target_present": "Y" if target_present else "N",
            "target_data_type": target_column.get("dataType", ""),
            "target_data_length_bytes": target_column.get("dataLengthBytes", ""),
            "target_precision": target_column.get("precision", ""),
            "target_scale": target_column.get("scale", ""),
            "target_char_length": target_column.get("charLength", ""),
            "target_char_used": target_column.get("charUsed", ""),
            "target_nullable": target_column.get("nullable", ""),
            "target_default": target_column.get("default", ""),
            "target_hidden": target_column.get("hidden", ""),
            "target_virtual": target_column.get("virtual", ""),
            "target_identity_generation": target_identity.get("generationType", ""),
            "target_identity_sequence": target_identity.get("sequenceName", ""),
            "definition_matches": matches,
            "backfill_review_candidate": "Y" if source_present and not target_present and source_non_nullable and not source_has_default else "N",
            "decision_owner": "",
            "backfill_required": "",
            "backfill_source": "",
            "transformation_rule": "",
            "affected_rows": "",
            "invalid_values": "",
            "temporary_nullable_decision": "",
            "approval_reference": "",
            "planned_ddl_dml": "",
            "validation_query": "",
            "rollback_plan": "",
            "review_status": "UNREVIEWED",
        }
        matrix_rows.append(row)
    changed_columns = []
    for table, column in sorted(shared_keys):
        differences = {
            field: {"source": source_columns[(table, column)].get(field), "target": target_columns[(table, column)].get(field)}
            for field in COLUMN_FIELDS
            if canonical(source_columns[(table, column)].get(field)) != canonical(target_columns[(table, column)].get(field))
        }
        if differences:
            changed_columns.append({"table": table, "column": column, "differences": differences})

    source_tables = {key(name) for name in source["Tables"]}
    target_tables = {key(name) for name in target["Tables"]}
    source_constraints = grouped_signatures(
        source["Constraints"], ("table", "name"), ("type", "status", "validated", "deleteRule", "searchCondition"),
        "position", source.get("Owner"),
    )
    target_constraints = grouped_signatures(
        target["Constraints"], ("table", "name"), ("type", "status", "validated", "deleteRule", "searchCondition"),
        "position", target.get("Owner"),
    )
    constraint_delta = {
        "structure": counter_delta(without_object_names(source_constraints), without_object_names(target_constraints)),
        "names": counter_delta(source_constraints, target_constraints),
    }
    source_indexes = grouped_signatures(source["Indexes"], ("table", "name"), ("uniqueness", "status"), "position")
    target_indexes = grouped_signatures(target["Indexes"], ("table", "name"), ("uniqueness", "status"), "position")
    index_delta = {
        "structure": counter_delta(without_object_names(source_indexes), without_object_names(target_indexes)),
        "names": counter_delta(source_indexes, target_indexes),
    }
    def row_signatures(rows: list[dict[str, Any]], fields: tuple[str, ...], local_owner: str | None = None) -> Counter[tuple[str, ...]]:
        signatures = Counter()
        for row in rows:
            values = []
            for field in fields:
                value = row.get(field)
                if field == "owner" and local_owner is not None:
                    values.append("LOCAL" if key(value) == key(local_owner) else "EXTERNAL")
                else:
                    values.append(key(value))
            signatures[tuple(values)] += 1
        return signatures

    identity_delta = counter_delta(
        # Oracle-generated ISEQ$$_ names are schema-local implementation details.
        # Compare the identity behavior, while retaining sequenceName in the CSV snapshots.
        row_signatures(source["Identities"], ("table", "column", "generationType")),
        row_signatures(target["Identities"], ("table", "column", "generationType")),
    )

    trigger_delta = counter_delta(
        row_signatures(source["Triggers"], ("table", "name", "status", "events", "type")),
        row_signatures(target["Triggers"], ("table", "name", "status", "events", "type")),
    )
    trigger_source_delta = counter_delta(
        row_signatures(source.get("TriggerSourceHashes", []), ("table", "name", "lineCount", "sha256")),
        row_signatures(target.get("TriggerSourceHashes", []), ("table", "name", "lineCount", "sha256")),
    )
    trigger_error_delta = counter_delta(
        row_signatures(source.get("TriggerErrors", []), ("name", "type", "line", "position", "text")),
        row_signatures(target.get("TriggerErrors", []), ("name", "type", "line", "position", "text")),
    )
    foreign_trigger_delta = counter_delta(
        row_signatures(source["ForeignTriggers"], ("table", "name", "owner", "status", "events", "type"), source.get("Owner")),
        row_signatures(target["ForeignTriggers"], ("table", "name", "owner", "status", "events", "type"), target.get("Owner")),
    )
    dependency_delta = counter_delta(
        row_signatures(source["StaticDependencies"], ("referencedTable", "name", "type", "dependencyType")),
        row_signatures(target["StaticDependencies"], ("referencedTable", "name", "type", "dependencyType")),
    )

    report = {
        "ReportVersion": 1,
        "CreatedAtUtc": datetime.now(timezone.utc).isoformat(),
        "Source": {"Owner": source.get("Owner"), "Snapshot": str(args.source)},
        "Target": {"Owner": target.get("Owner"), "Snapshot": str(args.target)},
        "SnapshotFreshness": {
            "SourceCapturedAtUtc": source.get("CapturedAtUtc"),
            "TargetCapturedAtUtc": target.get("CapturedAtUtc"),
            "CaptureSkewSeconds": capture_skew_seconds,
            "MaxCaptureSkewHours": args.max_capture_skew_hours,
            "Warnings": warnings,
        },
        "ReviewWarnings": trigger_source_warnings,
        "Summary": {
            "sourceTables": len(source_tables),
            "targetTables": len(target_tables),
            "missingTables": len(source_tables - target_tables),
            "extraTables": len(target_tables - source_tables),
            "missingColumns": len(source_keys - target_keys),
            "extraColumns": len(target_keys - source_keys),
            "changedColumns": len(changed_columns),
        },
        "Differences": {
            "missingTables": sorted(source_tables - target_tables),
            "extraTables": sorted(target_tables - source_tables),
            "missingColumns": [{"table": table, "column": column} for table, column in sorted(source_keys - target_keys)],
            "extraColumns": [{"table": table, "column": column} for table, column in sorted(target_keys - source_keys)],
            "changedColumns": changed_columns,
            "constraints": constraint_delta,
            "indexes": index_delta,
            "identities": identity_delta,
            "triggers": trigger_delta,
            "triggerSourceHashes": trigger_source_delta,
            "triggerErrors": trigger_error_delta,
            "foreignTriggers": foreign_trigger_delta,
            "staticDependencies": dependency_delta,
        },
        "Limitations": [
            "This is a metadata comparison only; it does not infer data backfills, approve DDL, or generate migration SQL.",
            "CHECK expressions use Oracle SEARCH_CONDITION_VC; any unavailable/truncated expression still needs direct Oracle metadata review.",
            "ALL_DEPENDENCIES does not include dynamic SQL or external integrations.",
            "Constraint/index structural differences and object-name differences are reported separately; identical structure with different names still needs operational compatibility review.",
            "Identity sequence names are retained as metadata but excluded from semantic identity comparison because Oracle-generated sequence names are schema-local.",
            "Trigger source is compared through SHA-256 and line count; the source text is not included in snapshots. Older snapshots without TriggerSourceHashes cannot prove trigger body equivalence.",
        ],
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    matrix_path.parent.mkdir(parents=True, exist_ok=True)
    with matrix_path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(matrix_rows[0].keys()) if matrix_rows else [])
        if matrix_rows:
            writer.writeheader()
            writer.writerows(matrix_rows)
    summary = report["Summary"]
    print(
        f"Compared {report['Source']['Owner']} -> {report['Target']['Owner']}: "
        f"tables missing/extra={summary['missingTables']}/{summary['extraTables']}, "
        f"columns missing/extra/changed={summary['missingColumns']}/{summary['extraColumns']}/{summary['changedColumns']}. "
        f"Reports: {args.output}, {matrix_path}; freshness warnings={len(warnings)}"
    )
    for warning in warnings:
        print(f"WARNING: {warning}")
    for warning in trigger_source_warnings:
        print(f"WARNING: {warning}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
