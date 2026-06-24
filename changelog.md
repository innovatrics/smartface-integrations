# Change Log

## 2026-06-24 - LockerMailer
Added a daily **Idle Locker Report**: at a configured time (default 09:00) it emails a table of assigned changing-room lockers (`Change Room Gents` / `Change Room Ladies`) not opened in more than `IdleDays` (default 14) to a dedicated recipient list. New `IdleLockerReportService` + `LockerMailer:IdleLockerReport` config section. Reuses the existing Dashboards data source and SMTP sender; does not release lockers.

## 2024-09-26 - AccessController
Upgraded to new version of a access controller, `src\Shared\AccessController\access_notification_service.proto` changed

## 2024-10-08 - AutoEnrollment
AutoEnrollment project released