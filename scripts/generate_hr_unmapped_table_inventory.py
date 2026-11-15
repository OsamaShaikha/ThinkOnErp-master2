from __future__ import annotations

from collections import defaultdict
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "artifacts" / "hr-audit" / "dev-template-schema-details.txt"
OUTPUT = ROOT / "docs" / "hr" / "HR_UNMAPPED_TABLE_METADATA_2026-11-13_AR.md"

TABLES = [
    "HR_ASSET_ASSIGNMENT",
    "HR_ATTENDANCE_RECORD",
    "HR_CANDIDATE",
    "HR_CANDIDATE_APP",
    "HR_CANDIDATE_APPLICATION",
    "HR_EMPLOYEE_DOCUMENT",
    "HR_EMPLOYMENT_CONTRACT",
    "HR_EMPLOYMENT_EVENT",
    "HR_EOS_PROVISION_ACCRUAL",
    "HR_EOS_SETTLEMENT",
    "HR_EXPENSE_CLAIM",
    "HR_EXPENSE_CLAIM_LINE",
    "HR_FINAL_SETTLEMENT",
    "HR_JOB_GRADE",
    "HR_JOB_REQUISITION",
    "HR_ONBOARDING_TASK",
    "HR_OVERTIME_RECORD",
    "HR_PAYROLL_LINE_COMP",
    "HR_SALARY_REVISION",
    "HR_STATUTORY_RULE",
    "HR_EMP_SALARY_STRUCTURE",
    "HR_EMP_SALARY_STRUCT_LINE",
    "HR_EMP_SHIFT_ASSIGNMENT",
]


def fields(line: str) -> list[str]:
    return [part.strip() for part in line.split(": ", 1)[1].split(" | ")]


def main() -> None:
    if not SOURCE.is_file():
        raise SystemExit(f"Missing schema snapshot: {SOURCE}")
    lines = SOURCE.read_text(encoding="utf-8").splitlines()
    if not any(line == "Connected; target schema DEV_TEMPLATE verified." for line in lines):
        raise SystemExit("The schema snapshot does not confirm DEV_TEMPLATE as its target.")
    if not any(line.startswith("Unmapped HR static dependency scan: completed within DEV_TEMPLATE only;") for line in lines):
        raise SystemExit("The schema snapshot does not contain the DEV_TEMPLATE static-dependency scan.")

    columns: dict[str, list[list[str]]] = defaultdict(list)
    constraints: dict[str, list[list[str]]] = defaultdict(list)
    indexes: dict[str, list[list[str]]] = defaultdict(list)
    row_counts: dict[str, int] = {}
    static_dependencies: dict[str, list[list[str]]] = defaultdict(list)
    all_constraints: list[list[str]] = []
    for line in lines:
        if line.startswith("Column [table,column,type,bytes,precision,scale,chars,char-used,nullable,default]: "):
            row = fields(line)
            if row[0] in TABLES:
                columns[row[0]].append(row)
        elif line.startswith("Constraint [table,name,type,status,validated,delete,column,position,ref-owner,ref-key]: "):
            row = fields(line)
            all_constraints.append(row)
            if row[0] in TABLES:
                constraints[row[0]].append(row)
        elif line.startswith("Index [table,name,uniqueness,status,column,position]: "):
            row = fields(line)
            if row[0] in TABLES:
                indexes[row[0]].append(row)
        elif line.startswith("Unmapped table [table,rows]: "):
            row = fields(line)
            if row[0] in TABLES:
                if row[0] in row_counts:
                    raise SystemExit(f"Duplicate aggregate row count for {row[0]}.")
                row_counts[row[0]] = int(row[1])
        elif line.startswith("Unmapped table static dependency [table,owner,name,type,dependency]: "):
            row = fields(line)
            if row[0] in TABLES:
                static_dependencies[row[0]].append(row)

    missing = [table for table in TABLES if not columns[table]]
    if missing:
        raise SystemExit("Tables missing from schema snapshot: " + ", ".join(missing))
    missing_counts = [table for table in TABLES if table not in row_counts]
    if missing_counts:
        raise SystemExit("Aggregate row counts missing from schema snapshot: " + ", ".join(missing_counts))

    keys: dict[str, tuple[str, list[str]]] = {}
    key_columns: dict[str, dict[str, list[tuple[int, str]]]] = defaultdict(lambda: defaultdict(list))
    for row in all_constraints:
        table, name, kind, _, _, _, column, position, _, _ = row
        if kind in {"P", "U"} and position not in {"NULL", ""}:
            key_columns[table][name].append((int(position), column))
    for table, groups in key_columns.items():
        for name, positioned_columns in groups.items():
            keys[name] = (table, [column for _, column in sorted(positioned_columns)])

    out = [
        "# تعريفات الجداول غير الممثلة مباشرة في EF — OPS-15",
        "",
        "**المصدر:** لقطة metadata للقراءة فقط من `DEV_TEMPLATE` بتاريخ 13 نوفمبر 2026: `artifacts/hr-audit/dev-template-schema-details.txt`.",
        "**النطاق:** أسماء الأعمدة وأنواعها وأطوالها وnullable/default، القيود الظاهرة، الفهارس، وأعداد الصفوف المجمعة بـ`COUNT(*)`. لم تُقرأ قيم السجلات ولم تُنفذ DDL/DML.",
        "**حدود الاستنتاج:** metadata والعدد لا يحددان المالك الوظيفي أو ما إذا كان الجدول نشطاً؛ لا يُستنتج أي تصنيف من الاسم وحده. لا تكشف اللقطة تعاقدات خارج المستودع أو علاقات التطبيق الديناميكية.",
        "",
        "| الجدول | عدد الصفوف | عدد الأعمدة | المفتاح الأساسي | المفاتيح الخارجية | الفهارس غير الأساسية |",
        "|---|---:|---:|---|---|---|",
    ]

    for table in TABLES:
        primary_keys = [row[1] for row in constraints[table] if row[2] == "P"]
        pk = [", ".join(keys[name][1]) if name in keys else name for name in primary_keys]
        fk_rows = [row for row in constraints[table] if row[2] == "R"]
        fks = sorted({f"{row[6]} → {keys[row[9]][0]}({', '.join(keys[row[9]][1])}) (delete {row[5]})"
                      if row[9] in keys else f"{row[6]} → {row[8]}.{row[9]} (delete {row[5]})" for row in fk_rows})
        index_groups: dict[str, list[str]] = defaultdict(list)
        uniqueness: dict[str, str] = {}
        status: dict[str, str] = {}
        for row in indexes[table]:
            index_groups[row[1]].append(f"{row[4]}#{row[5]}")
            uniqueness[row[1]] = row[2]
            status[row[1]] = row[3]
        non_pk_indexes = []
        for name in sorted(index_groups):
            if name not in primary_keys:
                cols = ",".join(col.split("#", 1)[0] for col in sorted(index_groups[name], key=lambda c: int(c.rsplit("#", 1)[1])))
                non_pk_indexes.append(f"{name} ({uniqueness[name]}, {status[name]}: {cols})")
        out.append(
            f"| `{table}` | {row_counts[table]} | {len(columns[table])} | {', '.join(f'`{name}`' for name in pk) or 'غير ظاهر'} | "
            f"{'; '.join(fks) or 'لا يوجد'} | {'; '.join(non_pk_indexes) or 'لا يوجد'} |"
        )

    for table in TABLES:
        out.extend(["", f"## `{table}`", "", "| العمود | النوع | القيود الظاهرة |", "|---|---|---|"])
        for row in columns[table]:
            _, name, data_type, byte_length, precision, scale, chars, char_used, nullable, default = row
            if data_type in {"NVARCHAR2", "VARCHAR2", "CHAR", "NCHAR"}:
                type_desc = f"{data_type}({chars} chars; {byte_length} bytes)"
            elif data_type == "NUMBER":
                type_desc = f"NUMBER({precision},{scale})"
            else:
                type_desc = data_type
            props = ["NULL" if nullable == "Y" else "NOT NULL"]
            if default and default != "NULL":
                props.append(f"default `{default}`")
            out.append(f"| `{name}` | {type_desc} | {'; '.join(props)} |")

        table_constraints: dict[str, dict[str, str]] = {}
        for row in constraints[table]:
            _, name, kind, enabled, validated, delete_rule, column, position, ref_owner, ref_key = row
            entry = table_constraints.setdefault(name, {"kind": kind, "enabled": enabled, "validated": validated, "delete": delete_rule, "owner": ref_owner, "ref": ref_key, "cols": ""})
            if position not in {"NULL", ""}:
                entry["cols"] += ("," if entry["cols"] else "") + column
        out.extend(["", "القيود:"])
        if table_constraints:
            for name, detail in sorted(table_constraints.items()):
                kind = {"P": "PK", "U": "UNIQUE", "R": "FK", "C": "CHECK/NOT NULL"}.get(detail["kind"], detail["kind"])
                target = f" → {detail['owner']}.{detail['ref']} (delete {detail['delete']})" if detail["kind"] == "R" else ""
                cols = f" [{detail['cols']}]" if detail["cols"] else ""
                out.append(f"- `{name}`: {kind}{cols}, {detail['enabled']}/{detail['validated']}{target}.")
        else:
            out.append("- لا قيود مسجلة في اللقطة.")
        if any(row[2] == "C" for row in constraints[table]):
            out.append("- تعرض اللقطة قيود C وأعمدتها، لكنها لا تسجل نص تعبير CHECK؛ لا تُستنتج شروط القيم من هذا الملخص.")

    out.extend([
        "",
        "## اعتماديات Oracle الثابتة داخل DEV_TEMPLATE",
        "",
        "النتيجة التالية من `ALL_DEPENDENCIES` حيث المالك والمشار إليه كلاهما `DEV_TEMPLATE`. لا تشمل SQL الديناميكي أو الكائنات المملوكة لمخططات أخرى؛ غياب صف هنا لا يثبت عدم الاستخدام.",
        "",
        "| الجدول | الكائن المعتمد عليه | النوع | نوع الاعتماد |",
        "|---|---|---|---|",
    ])
    for table in TABLES:
        deps = static_dependencies.get(table, [])
        if deps:
            for _, owner, name, object_type, dependency_type in deps:
                out.append(f"| `{table}` | `{owner}.{name}` | {object_type} | {dependency_type} |")
        else:
            out.append(f"| `{table}` | لم يظهر اعتماد ثابت في DEV_TEMPLATE | — | — |")

    out.extend([
        "",
        "## ما يلزم للمراجعة الوظيفية",
        "",
        "تحتاج كل حالة إلى مطابقة هذا المخطط وعدد الصفوف الحالي مع مالك العمل، النظام المرجعي، محتوى بيانات مصنف ومصرح بمراجعته، ومستهلكات العميل/التكامل. العدد لقطة لحظية لـ`DEV_TEMPLATE` ولا يثبت نشاط الجدول أو خلو الاعتماديات خارج قاعدة البيانات. تبقى الجداول محفوظة حتى تعتمد الملكية والنطاق وخطة الاحتفاظ أو الترحيل.",
        "",
        "هذا الملخص يوسع دليل OPS-15 التقني ولا يغير قرارات نطاق HR أو يثبت وجوب mapping لأي جدول.",
    ])
    OUTPUT.write_text("\n".join(out) + "\n", encoding="utf-8")
    print(f"Wrote {OUTPUT.relative_to(ROOT)} ({len(TABLES)} tables; {sum(map(len, columns.values()))} columns; {sum(row_counts.values())} aggregate rows; {sum(map(len, static_dependencies.values()))} static dependency rows).")


if __name__ == "__main__":
    main()
