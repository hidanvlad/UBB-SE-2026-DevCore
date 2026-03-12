## 1) [Doctor Schedule] Create Doctor Schedule page and calendar container
**Type:** Feature  
**Priority:** High

### Description
Create a dedicated Doctor Schedule page where doctors can view upcoming appointments and shifts in a calendar interface.

### Acceptance Criteria
- Doctor can access the page from authenticated doctor navigation.
- Page includes calendar container with date navigation (today/next/previous).
- Loading, empty, and error states are implemented.
- Non-doctor users are blocked from access.

---

## 2) [Doctor Schedule] Implement API/service to fetch upcoming doctor appointments
**Type:** Backend / Feature  
**Priority:** High

### Description
Implement data retrieval for upcoming appointments for the logged-in doctor.

### Acceptance Criteria
- Endpoint/service returns only appointments for current doctor.
- Response includes: appointment id, date, start time, end time, status, and location.
- Only future appointments are returned by default.
- Proper error handling and pagination/windowing are included if dataset is large.

---

## 3) [Doctor Schedule] Render upcoming appointments in calendar component
**Type:** Frontend / Feature  
**Priority:** High

### Description
Bind appointment data to calendar events so doctors can visually see upcoming scheduled medical appointments.

### Acceptance Criteria
- Appointments render correctly in day/week calendar views.
- Overlapping appointments are displayed without layout breakage.
- Clicking an appointment opens details panel/modal.
- Appointment status/type is visually distinguishable (color/badge/tag).

---

## 4) [Doctor Schedule] Add daily/weekly shift roster generation
**Type:** Backend + Frontend / Feature  
**Priority:** High

### Description
Enable generation and display of on-duty shift roster in daily and weekly modes.

### Acceptance Criteria
- User can switch between Daily and Weekly roster view.
- Shift data is grouped/sorted chronologically.
- Multiple shifts per day are supported.
- Empty-day/empty-week states are handled clearly.

---

## 5) [Doctor Schedule] Display exact shift locations (ER/Clinic Room/etc.)
**Type:** Feature  
**Priority:** Medium

### Description
Expose and display shift location details for each shift entry and calendar block.

### Acceptance Criteria
- Each shift includes a visible location label.
- Location appears in both roster list and shift detail popover/modal.
- Supports values like ER, ICU, Clinic Room X.
- Missing location uses fallback text (e.g., “Location TBD”).

---

## 6) [Doctor Schedule] Show shift start/end times as exact calendar time blocks
**Type:** Frontend / Feature  
**Priority:** High

### Description
Render shifts using precise start/end boundaries in the calendar timeline.

### Acceptance Criteria
- Each shift displays Shift Start Time and Shift End Time.
- Time blocks align correctly with timeline grid.
- Overnight shifts (crossing midnight) render correctly.
- Time formatting respects configured locale (12h/24h).

---

## 7) [Doctor Schedule] Enforce authorization and scheduling validation rules
**Type:** Security / Backend  
**Priority:** High

### Description
Ensure schedule privacy and data correctness for appointments and shifts.

### Acceptance Criteria
- Doctor can only access their own schedule data.
- Unauthorized access attempts are rejected and logged.
- Validation ensures shift start < shift end.
- Invalid/overlapping/duplicated malformed records are safely handled.

---

## 8) [Doctor Schedule] Add automated tests and acceptance coverage for schedule module
**Type:** QA / Test Automation  
**Priority:** High

### Description
Add test coverage to guarantee reliability of the Doctor Schedule functionality.

### Acceptance Criteria
- Unit tests cover mapping/transformation of appointment and shift data.
- Integration tests cover API + UI rendering path.
- E2E tests verify:
  - appointments visible in calendar,
  - daily/weekly roster toggle,
  - shift location visibility,
  - exact shift time block rendering.
- Regression checklist is documented in PR template or test plan.

---

## 9) [Pharmacy Schedule] Create Pharmacy Schedule view and shift calendar/roster container
**Type:** Feature  
**Priority:** High

### Description
Create a dedicated Pharmacy Schedule view where pharmacists can see work shifts and pharmacy rotations.

### Acceptance Criteria
- Pharmacist can access Pharmacy Schedule from authenticated pharmacist navigation.
- View supports date navigation and initial default range.
- Loading, empty, and error states are implemented.
- Non-pharmacist users are restricted from this view.

---

## 10) [Pharmacy Schedule] Implement API/service to fetch pharmacist shifts and rotations
**Type:** Backend / Feature  
**Priority:** High

### Description
Implement retrieval of scheduled shifts and pharmacy rotation assignments for the logged-in pharmacist.

### Acceptance Criteria
- Endpoint/service returns only shifts for current pharmacist.
- Response includes: shift id, rotation assignment, start time, end time, status, and location/unit if available.
- Supports date-range filtering for daily and weekly views.
- Handles large datasets via pagination/windowing where required.

---

## 11) [Pharmacy Schedule] Add daily/weekly roster toggle and rendering for pharmacist shifts
**Type:** Frontend / Feature  
**Priority:** High

### Description
Enable pharmacists to switch between daily and weekly roster modes and view scheduled work shifts.

### Acceptance Criteria
- Toggle between Daily and Weekly roster works without full-page reload.
- Shifts are grouped and sorted chronologically.
- Multiple shifts in a single day are displayed correctly.
- Empty day/week states show clear messaging.

---

## 12) [Pharmacy Schedule] Display pharmacy rotation assignments on each shift
**Type:** Feature  
**Priority:** High

### Description
Show each pharmacist’s specific rotation assignment (e.g., Inpatient, Outpatient, IV Room, Clinical Rotation) as part of shift details.

### Acceptance Criteria
- Every shift displays its rotation assignment label.
- Rotation is visible in both roster rows and shift detail panel/modal.
- Missing rotation uses fallback text (e.g., “Rotation TBD”).
- Rotation values are consistent with backend master data.

---

## 13) [Pharmacy Schedule] Show exact shift duration using Shift Start Time and Shift End Time
**Type:** Frontend / Feature  
**Priority:** High

### Description
Display exact shift timing and duration clearly for every scheduled shift.

### Acceptance Criteria
- Shift Start Time and Shift End Time are always visible for each shift.
- Duration is computed and displayed accurately (e.g., 8h 30m).
- Overnight shifts crossing midnight are calculated correctly.
- Time display respects locale/time format settings (12h/24h).

---

## 14) [Pharmacy Schedule] Add shift status indicators (Scheduled, Active, Completed)
**Type:** Feature  
**Priority:** High

### Description
Display real-time/current status of scheduled shifts with distinct visual indicators.

### Acceptance Criteria
- Status values include at least: Scheduled, Active, Completed.
- Status is visible in roster and shift details.
- Status styling (badge/color/icon) is consistent and accessible.
- Invalid or unknown statuses fall back to a safe default (e.g., “Unknown”).

---

## 15) [Pharmacy Schedule] Enforce role-based access and shift data validation
**Type:** Security / Backend  
**Priority:** High

### Description
Ensure pharmacists only access their own schedule and shift records meet integrity rules.

### Acceptance Criteria
- Pharmacist can only retrieve their own shifts/rotations.
- Unauthorized requests are rejected and auditable.
- Validation ensures start time < end time.
- Duplicate/malformed shift records are handled safely and logged.

---

## 16) [Pharmacy Schedule] Add automated tests for roster, rotation, duration, and status flows
**Type:** QA / Test Automation  
**Priority:** High

### Description
Add test coverage for pharmacist scheduling to ensure correctness and prevent regressions.

### Acceptance Criteria
- Unit tests cover shift mapping, duration calculations, and status derivation.
- Integration tests verify API-to-UI rendering for daily/weekly views.
- E2E tests verify:
  - daily/weekly roster visibility,
  - rotation assignment display,
  - start/end time and duration display,
  - status badges for Scheduled/Active/Completed.
- Regression checklist is documented for PR review.
