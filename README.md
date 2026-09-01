# Student Management Information System

A normalized SQL Server database with a C# console application front end, built for the Information Systems & Database Systems class project.

The college previously kept student, course, enrolment, lecturer, payment and results data in separate spreadsheets. This produced duplicate records, inconsistent information and slow reporting. This system stores each fact once in a 3NF database and gives every role a single source of truth.

---

## Tech stack

| Layer | Technology |
|---|---|
| Database | Microsoft SQL Server |
| Application | C# console application (.NET 8) |
| Data access | `Microsoft.Data.SqlClient` |
| Tools | SQL Server Management Studio, Visual Studio 2022 |

---

## Project structure

```
├── 01_BUILD.sql              Creates the database, tables, sample data,
│                             view, stored procedures and trigger
├── 02_QUERIES.sql            The ten required SQL queries
├── 03_DEMO_AND_TESTS.sql     Transaction demo, trigger demo and 9 test cases
├── Program.cs                The console application
├── appsettings.json          Connection string (not committed)
├── StudentManagementApp.csproj
├── ERD.png / ERD.svg         Entity Relationship Diagram
└── README.md
```

---

## Setup

### 1. Build the database

Open `01_BUILD.sql` in SSMS and press **Execute** once. The script drops and recreates the database, so it can be re-run at any time to reset to a known state.

It creates six tables, loads the sample data, and installs the view, both stored procedures and the audit trigger.

### 2. Configure the connection

Create `appsettings.json` next to the project file:

```json
{
  "ConnectionStrings": {
    "StudentManagementDB": "Server=YOUR-PC\\YOUR-INSTANCE;Database=StudentManagementDB;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

Replace `YOUR-PC\YOUR-INSTANCE` with the server name shown in the SSMS "Connect to Server" dialog. In JSON the backslash must be doubled.

The application reads the environment variable `SMIS_CONNECTION` first and only falls back to this file, so no credentials need to be committed. `appsettings.json` is listed in `.gitignore`.

### 3. Run the application

**Visual Studio:** open the project, ensure the `Microsoft.Data.SqlClient` NuGet package is installed, set `appsettings.json` → Copy to Output Directory → *Copy if newer*, then press **F5**.

**Command line:**

```bash
dotnet restore
dotnet run
```

---

## Features

```
STUDENT MANAGEMENT SYSTEM

1. Display all students          6. View student results
2. Search for a student          7. View students without enrolments
3. Register a student            8. Record a payment
4. Enrol a student               9. Exit
5. Capture or update a mark
```

Every user-supplied value is passed as a typed `SqlParameter`, all connections and readers are wrapped in `using` statements, `SqlException` numbers are translated into readable messages, and database NULLs are handled with `IsDBNull`.

---

## Database design

Five core tables plus an audit table, in Third Normal Form.

| Table | Key points |
|---|---|
| `STUDENT` | `StudentID` PK · unique student number and email · status limited to Active / Inactive |
| `LECTURER` | `LecturerID` PK · unique email |
| `COURSE` | `CourseID` PK · unique course code · `LecturerID` FK |
| `ENROLMENT` | Composite PK `(StudentID, CourseID)` · mark constrained to 0–100 |
| `PAYMENT` | `PaymentID` PK · `StudentID` FK · amount must be greater than zero · unique reference |
| `MARK_AUDIT` | Written only by the trigger; deliberately has no foreign keys so history survives deletions |

`STUDENT` and `COURSE` are many-to-many. `ENROLMENT` resolves this as a bridge entity, and its composite primary key is what prevents a student from being enrolled in the same course twice.

See `ERD.png` for the full diagram.

### Database objects

| Object | Purpose |
|---|---|
| `usp_EnrolStudent` | Enrols a student and sets their status to Active inside a TRY/CATCH transaction. Both operations succeed or neither does; on failure it checks `@@TRANCOUNT`, rolls back and re-throws the original error. |
| `vw_StudentResults` | Reporting view returning student, course, mark and a Pass / NYC result. |
| `usp_GetStudentResults` | Returns all results for one student; called by menu option 6. |
| `trg_Enrolment_MarkAudit` | AFTER UPDATE trigger recording previous mark, new mark, date and user. Joins `inserted` to `deleted` as sets, so multi-row updates are logged correctly. |

### Fee rule

Each enrolment costs **R5 000.00**. Outstanding fees are calculated as `(number of enrolments × 5000) − total payments`.

---

## Testing

Run `03_DEMO_AND_TESTS.sql` one block at a time.

| Test | Expected result | Enforced by |
|---|---|---|
| Register a valid student | Record created | — |
| Duplicate student number | Rejected | `UQ_Student_Number` |
| Enrol an existing student | Enrolment created, status Active | `usp_EnrolStudent` |
| Enrol the same student twice | Rejected | `PK_Enrolment` |
| Capture a mark of 78 | Saved | — |
| Capture a mark of 120 | Rejected | `CK_Enrolment_Mark` |
| Search for a non-existent student | Friendly not-found message | Application |
| Update an existing mark | Mark changes, audit row created | `trg_Enrolment_MarkAudit` |
| Force an error mid-transaction | Everything rolled back | TRY/CATCH + `@@TRANCOUNT` |

---

## Learning outcomes covered

- Analysing an organizational information problem and identifying the five Information System components
- Designing a relational database in Third Normal Form
- Creating tables with appropriate keys, constraints and relationships
- Using JOINs, transactions, views, stored procedures and triggers
- Connecting a C# application securely to SQL Server using parameterized commands
- Testing and demonstrating an end-to-end database solution

---
