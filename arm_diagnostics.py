#!/usr/bin/env python
"""
Minimal Faro Arm diagnostic scripts.

Tested with:
  - FaroArmDriver-ml-6.5.1.4-Full.exe from https://knowledge.faro.com/Hardware/FaroArm_and_ScanArm/FaroArm_and_ScanArm/Driver_for_the_USB_FaroArm-ScanArm-Gage#Earlier_Drivers-15878

Requires:
  - pythonnet (pip install pythonnet==3.0.*)
  - Faro driver/manager already installed and RUNNING
    -- ^^^ ensure it's actually running and already showing
       data on the FaroManager diagnostics screen
  - 64-bit Python to match the 64-bit Faro assemblies
"""

import argparse
import logging
import os
import sys
import threading
import time
from pathlib import Path
from typing import Optional
import shutil


LOGGER = logging.getLogger("arm_diagnostics")


class StatusDisplay:
    def __init__(self) -> None:
        self._lock = threading.Lock()
        self._last_text = ""
        self._last_length = 0
        self._active = False
        self._last_width = shutil.get_terminal_size(fallback=(120, 24)).columns

    def write(self, text: str) -> None:
        if text is None:
            return
        text = text.replace("\n", " ")
        width = shutil.get_terminal_size(fallback=(self._last_width or 120, 24)).columns
        self._last_width = width
        max_len = max(20, width - 1)
        if len(text) > max_len:
            text = text[: max_len - 3] + "..."
        with self._lock:
            sys.stdout.write("\r" + text)
            if len(text) < self._last_length:
                sys.stdout.write(" " * (self._last_length - len(text)))
            sys.stdout.flush()
            self._last_text = text
            self._last_length = len(text)
            self._active = True

    def interrupt(self) -> None:
        with self._lock:
            sys.stdout.write("\r")
            if self._active:
                sys.stdout.write(" " * self._last_length + "\r")
                self._active = False
            sys.stdout.flush()

    def restore(self) -> None:
        with self._lock:
            if not self._last_text:
                return
            sys.stdout.write("\r" + self._last_text)
            if len(self._last_text) < self._last_length:
                sys.stdout.write(" " * (self._last_length - len(self._last_text)))
            sys.stdout.flush()
            self._active = True

    def clear(self) -> None:
        with self._lock:
            sys.stdout.write("\r" + " " * self._last_length + "\r")
            sys.stdout.flush()
            self._last_text = ""
            self._last_length = 0
            self._active = False


STATUS_DISPLAY = StatusDisplay()


class StatusAwareStreamHandler(logging.StreamHandler):
    def __init__(self) -> None:
        super().__init__(stream=sys.stdout)

    def emit(self, record: logging.LogRecord) -> None:
        STATUS_DISPLAY.interrupt()
        try:
            super().emit(record)
        finally:
            STATUS_DISPLAY.restore()


def add_reference(path: Path) -> None:
    LOGGER.debug("Adding Faro .NET references from %s", path)
    try:
        import clr  # pythonnet
    except ModuleNotFoundError as exc:
        raise RuntimeError(
            "pythonnet is not installed. Install it with 'pip install pythonnet==3.0.*' "
            "using the same Python interpreter that runs this script."
        ) from exc

    if hasattr(os, "add_dll_directory"):
        os.add_dll_directory(str(path))
        LOGGER.debug("Registered %s with DLL search path", path)
    if str(path) not in sys.path:
        sys.path.insert(0, str(path))
        LOGGER.debug("Inserted %s into sys.path", path)
    clr.AddReference(str(path / "FaroArm.Interfaces.dll"))
    clr.AddReference(str(path / "FaroArm.Net.dll"))
    LOGGER.debug("Successfully loaded FaroArm .NET assemblies")


class FaroArmListener:
    def __init__(self, driver_dir: Path) -> None:
        LOGGER.info("Initializing FaroArmListener with driver_dir=%s", driver_dir)
        add_reference(driver_dir)

        from FaroArm import FaroArmInterfaceFactory
        from FaroArm.Interfaces import FaroArmUpdateEventArgs  # noqa: F401  (keeps delegate type loaded)

        self._factory = FaroArmInterfaceFactory()
        LOGGER.debug("Created FaroArmInterfaceFactory: %s", self._factory)
        self._manager = self._factory.CreateIFaroArmManager()
        LOGGER.debug("Manager instance: %s", self._manager)
        LOGGER.info("Connecting to Faro Arm manager")
        self._manager.Connect()

        self._arm = self._manager.GetFirstDetectedFaroArm()
        if self._arm is None:
            raise RuntimeError("No Faro arm detected. Ensure the arm is powered and the Faro Manager is running.")

        LOGGER.info("Connected to Faro arm: %s", self._describe_arm(self._arm))

        self._update_count = 0
        self._last_button_state = None
        self._last_receive_time = None
        self._avg_period = None
        self._start_time = time.perf_counter()
        self._shutdown_event = threading.Event()

        self._handler = self._build_handler()
        self._arm.FaroArmUpdate += self._handler
        LOGGER.debug("Subscribed to FaroArmUpdate events")
        self._arm.EnableFaroArmUpdates(True)
        LOGGER.info("Faro arm updates enabled")
        if hasattr(self._arm, "IsFaroArmUpdatesEnabled"):
            try:
                LOGGER.debug("Driver reports updates enabled: %s", self._arm.IsFaroArmUpdatesEnabled)
            except Exception:
                LOGGER.debug("Driver does not expose IsFaroArmUpdatesEnabled property")

        self._watchdog = threading.Thread(
            target=self._monitor_updates,
            name="FaroArmUpdateWatchdog",
            daemon=True,
        )
        self._watchdog.start()

    def _build_handler(self):
        def handler(sender, event_args):
            update = event_args.FaroArmUpdate
            self._update_count += 1
            now = time.perf_counter()
            dt = None
            if self._last_receive_time is not None:
                dt = now - self._last_receive_time
                if self._avg_period is None:
                    self._avg_period = dt
                else:
                    self._avg_period = (0.9 * self._avg_period) + (0.1 * dt)
                if dt > 0.5:
                    LOGGER.warning("Gap between Faro arm updates: %.3f s", dt)
            else:
                LOGGER.debug("First update callback received")

            self._last_receive_time = now
            uptime = now - self._start_time
            STATUS_DISPLAY.write(self._format_status(update, dt, uptime))

            if self._update_count == 1:
                attrs = [attr for attr in dir(update) if not attr.startswith("_")]
                preview = ", ".join(attrs[:25])
                if len(attrs) > 25:
                    preview += ", ..."
                LOGGER.debug("First update sender type: %s", type(sender).__name__)
                LOGGER.debug("Update payload exposes %d attributes: %s", len(attrs), preview)
                LOGGER.info("First live update received from Faro arm")
            buttons = getattr(update, "Buttons", None)
            if buttons != self._last_button_state:
                LOGGER.info("Button state changed: %s -> %s", self._last_button_state, buttons)
                self._last_button_state = buttons

        return handler

    def _format_status(self, update, dt: Optional[float], uptime: float) -> str:
        def safe_float(value) -> str:
            try:
                return f"{float(value):8.3f}"
            except (TypeError, ValueError):
                return "   n/a "
            except Exception:
                return "   err "

        dt_ms = f"{dt * 1000:6.1f}ms" if dt is not None else "   --- "
        avg_hz = "--.-Hz"
        if self._avg_period and self._avg_period > 0:
            avg_hz = f"{1.0 / self._avg_period:6.2f}Hz"

        buttons = getattr(update, "Buttons", None)
        buttons_display = str(buttons) if buttons is not None else "-"

        angles = []
        for idx in range(1, 8):
            attr = f"Angle{idx}"
            if hasattr(update, attr):
                try:
                    angles.append(f"{float(getattr(update, attr)):6.2f}")
                except (TypeError, ValueError):
                    angles.append("  n/a")
                except Exception:
                    angles.append("  err")
        angles_display = ", ".join(angles) if angles else "n/a"

        status = (
            f"#{self._update_count:06d} | ts={getattr(update, 'TimeStamp', '-')!s:>10} | "
            f"XYZ=({safe_float(getattr(update, 'X', None))}, {safe_float(getattr(update, 'Y', None))}, "
            f"{safe_float(getattr(update, 'Z', None))}) | "
            f"ABC=({safe_float(getattr(update, 'A', None))}, {safe_float(getattr(update, 'B', None))}, "
            f"{safe_float(getattr(update, 'C', None))}) | "
            f"Angles=({angles_display}) | Buttons={buttons_display} | dt={dt_ms} | "
            f"avg={avg_hz} | uptime={uptime:6.1f}s"
        )
        collapsed = status.replace("\n", " ")
        return collapsed[:512]

    def _monitor_updates(self) -> None:
        last_count = 0
        last_warning_time = 0.0
        while not self._shutdown_event.wait(1.0):
            now = time.perf_counter()
            elapsed_since_start = now - self._start_time

            if self._update_count == 0 and elapsed_since_start > 2.0:
                if now - last_warning_time > 5.0:
                    LOGGER.warning(
                        "No Faro arm updates received %.1f s after enabling updates. "
                        "Verify the Faro Manager is streaming and the probe is active.",
                        elapsed_since_start,
                    )
                    last_warning_time = now
                continue

            if self._update_count == last_count:
                if self._last_receive_time is not None and now - self._last_receive_time > 3.0:
                    if now - last_warning_time > 5.0:
                        LOGGER.warning(
                            "Faro arm updates have stalled for %.1f s. "
                            "Move the arm or check device state.",
                            now - self._last_receive_time,
                        )
                        last_warning_time = now
            else:
                last_count = self._update_count

    def close(self) -> None:
        LOGGER.info("Shutting down FaroArmListener")
        self._shutdown_event.set()
        try:
            LOGGER.debug("Disabling Faro arm updates")
            self._arm.EnableFaroArmUpdates(False)
        except Exception:
            pass
        try:
            LOGGER.debug("Unsubscribing from FaroArmUpdate events")
            self._arm.FaroArmUpdate -= self._handler
        except Exception:
            pass
        try:
            LOGGER.info("Disconnecting from Faro manager")
            self._manager.Disconnect()
        except Exception:
            pass
        if hasattr(self, "_watchdog") and self._watchdog.is_alive():
            self._watchdog.join(timeout=1.0)

        LOGGER.debug("Total updates processed before shutdown: %d", self._update_count)
        STATUS_DISPLAY.clear()

    def _describe_arm(self, arm) -> str:
        attributes = []
        for attr in ("ModelName", "SerialNumber", "FirmwareVersion", "SoftwareVersion"):
            value = getattr(arm, attr, None)
            if value:
                attributes.append(f"{attr}={value}")
        if not attributes:
            return f"{arm}"
        return ", ".join(attributes)


def main() -> None:
    parser = argparse.ArgumentParser(description="Print live pose data from a Faro Arm via the official Faro driver.")
    parser.add_argument(
        "--driver-dir",
        default=r"C:\Program Files\Common Files\FARO Shared",
        help="Directory containing FaroArm.Interfaces.dll (default: %(default)s)",
    )
    parser.add_argument(
        "--log-level",
        default="DEBUG",
        help=(
            "Python logging level (e.g. DEBUG, INFO, WARNING). "
            "Defaults to %(default)s."
        ),
    )
    args = parser.parse_args()
    log_level = getattr(logging, args.log_level.upper(), None)
    if not isinstance(log_level, int):
        parser.error(f"Invalid log level: {args.log_level}")
    logging.basicConfig(
        level=log_level,
        format="%(asctime)s | %(levelname)s | %(name)s | %(message)s",
        handlers=[StatusAwareStreamHandler()],
        force=True,
    )
    LOGGER.debug("Parsed CLI arguments: %s", args)
    LOGGER.debug("Logger initialized at level %s", args.log_level.upper())
    LOGGER.debug("Python version: %s", sys.version.replace("\n", " "))

    driver_dir = Path(args.driver_dir)
    if not driver_dir.exists():
        parser.error(f"Faro driver directory not found: {driver_dir}")

    LOGGER.info("Using Faro driver directory: %s", driver_dir)
    listener = FaroArmListener(driver_dir)

    stop_event = threading.Event()
    LOGGER.debug("Stop event created; entering listener loop")
    LOGGER.info("Listening for Faro arm updates. Press Ctrl+C to exit.")
    STATUS_DISPLAY.write("Waiting for Faro arm updates...")
    try:
        while not stop_event.wait(0.2):
            pass
    except KeyboardInterrupt:
        LOGGER.info("Keyboard interrupt received; shutting down")
    finally:
        listener.close()
        LOGGER.info("FaroArmListener shutdown complete")


if __name__ == "__main__":
    main()
