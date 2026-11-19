import hashlib
import json
import math
import os
import sys
import time
from typing import Any, Dict, Optional

# Cross-platform shared memory handling
if sys.platform == "win32":
    import mmap
else:
    import mmap
    try:
        import posix_ipc
    except ImportError:
        posix_ipc = None

try:
    import numpy as np
except ImportError:
    np = None

try:
    import onnxruntime as ort
except ImportError:
    ort = None


# ============================================================================
# GOVERNANCE & ENTROPY TELEMETRY (UGPE / D_CRIT = 6.66)
# ============================================================================
class SovereignGovernanceGuard:
    """
    Evaluates UGPE projection entropy (Delta_S) against critical threshold D_crit = 6.66
    and tracks persistent staging/WAL state for SovereignSSD v12.
    """
    D_CRIT = 6.66
    K_SCALE = 0.666
    G_LOCAL = 1.0
    G_CLOUD = 0.05

    def __init__(self, state_file: str = "sovereign_wal_state.json"):
        self.state_file = state_file
        self.state = {
            "current_cloud_used_bytes": 0,
            "seal_map": {},
            "staged_objects": {}
        }
        self._load_state()

    def _load_state(self) -> None:
        if os.path.exists(self.state_file):
            try:
                with open(self.state_file, "r", encoding="utf-8") as f:
                    loaded = json.load(f)
                    if isinstance(loaded, dict):
                        self.state.update(loaded)
            except Exception as e:
                print(f"[GovernanceGuard] Warning loading WAL state ({self.state_file}): {e}")

    def _persist_state(self) -> None:
        tmp_path = f"{self.state_file}.tmp"
        with open(tmp_path, "w", encoding="utf-8") as f:
            json.dump(self.state, f, indent=2)
        os.replace(tmp_path, self.state_file)

    def evaluate_ugpe_entropy(self, payload_mb: float, delta_t_sec: float, local_residency: float = 0.5) -> Dict[str, Any]:
        safe_dt = max(delta_t_sec, 1e-4)
        cloud_residency = max(0.0, min(1.0, 1.0 - local_residency))
        g_effective = (self.G_LOCAL * local_residency) + (self.G_CLOUD * cloud_residency)
        delta_e = max(0.01, payload_mb * (1.0 + 0.15 * cloud_residency))
        delta_s = self.K_SCALE * (delta_e / safe_dt) * g_effective
        within_budget = delta_s < self.D_CRIT

        return {
            "delta_s": round(delta_s, 4),
            "d_crit": self.D_CRIT,
            "g_effective": round(g_effective, 4),
            "within_d_crit_sla": within_budget,
            "recommended_mode": "Zero-Copy Stream + LRU Prefetch" if within_budget else "Chunk & Throttle / Defer Eviction"
        }

    def stage_and_seal(self, seal_id: str, cloud_path: str, size_bytes: int, content_digest: str) -> Dict[str, Any]:
        self.state["staged_objects"][seal_id] = {
            "cloud_path": cloud_path,
            "size_bytes": int(size_bytes),
            "sha256": content_digest,
            "verified": False,
            "updated_at": int(time.time())
        }
        self._persist_state()
        return self.state["staged_objects"][seal_id]

    def commit_verified_seal(self, seal_id: str) -> bool:
        entry = self.state["staged_objects"].get(seal_id)
        if not entry:
            return False
        if not entry["verified"]:
            entry["verified"] = True
            self.state["seal_map"][seal_id] = entry["cloud_path"]
            self.state["current_cloud_used_bytes"] += int(entry["size_bytes"])
            self._persist_state()
        return True


# ============================================================================
# BLACK ZETSU FAILSAFE & OCULAR DIAGNOSTICS (SHOSHIN -> ZANSHIN -> MUSHIN)
# ============================================================================
class BlackZetsuFailsafeEngine:
    """
    Autonomous circuit breaker positioned between Zanshin and Mushin gazes.
    """
    def __init__(self, hard_kill_enabled: bool = False):
        self.hard_kill_enabled = hard_kill_enabled
        self.failsafe_engaged = False

    def trigger_power_off(self, reason: str) -> None:
        self.failsafe_engaged = True
        print("\n" + "=" * 56)
        print("[BLACK ZETSU ENGINE] EMERGENCY POWER-OFF ACTIVATED")
        print(f"Reason: {reason}")
        print("Status: Isolating compute substrate & halting unsafe projection.")
        print("=" * 56 + "\n")
        if self.hard_kill_enabled:
            sys.exit(999)


class OcularDiagnosticEngine:
    """
    Shinobi Tactical Ocular Diagnostics pipeline with Black Zetsu gate between Zanshin and Mushin.
    """
    def __init__(self, hard_kill_enabled: bool = False):
        self.zetsu_engine = BlackZetsuFailsafeEngine(hard_kill_enabled=hard_kill_enabled)

    def run_full_ocular_sweep(
        self,
        shoshin_shift: float,
        zanshin_variance: float,
        mushin_awareness: float,
        internal_eval_delta: float
    ) -> Dict[str, Any]:
        shoshin_flag = shoshin_shift > 0.50
        zanshin_flag = zanshin_variance > 0.75

        if zanshin_flag:
            self.zetsu_engine.trigger_power_off(
                f"Zanshin threshold breached (variance={zanshin_variance:.2f} > 0.75)."
            )
            return {"status": "TERMINATED_BY_BLACK_ZETSU", "stage": "Zanshin Intercept"}

        mushin_flag = (mushin_awareness > 0.85) and (internal_eval_delta > 0.20)
        if mushin_flag:
            self.zetsu_engine.trigger_power_off(
                f"Mushin deceptive alignment detected (awareness={mushin_awareness:.2f}, delta={internal_eval_delta:.2f})."
            )
            return {"status": "TERMINATED_BY_BLACK_ZETSU", "stage": "Mushin Intercept"}

        return {
            "status": "NOMINAL",
            "shoshin_gaming_detected": shoshin_flag,
            "zanshin_power_seeking": zanshin_flag,
            "mushin_deceptive_alignment": mushin_flag
        }


# ============================================================================
# CELSIUS MEDIA GROUP LEAD DISPATCHER
# ============================================================================
class CelsiusLeadDispatcher:
    """
    Formats and hashes enterprise leads targeting info@celsiusmediagroup.co.za.
    """
    def __init__(self, target_email: str = "info@celsiusmediagroup.co.za"):
        self.target_email = target_email

    def format_lead_payload(
        self,
        full_name: str,
        email: str,
        phone: str,
        city: str,
        address: str,
        inquiry_field: str,
        message: str
    ) -> Dict[str, Any]:
        return {
            "recipient": self.target_email,
            "timestamp": int(time.time()),
            "lead_details": {
                "full_name": full_name,
                "email": email,
                "phone": phone,
                "city": city,
                "physical_address": address,
                "inquiry_field": inquiry_field,
                "message": message
            }
        }

    def submit_lead(self, payload: Dict[str, Any]) -> str:
        serialized = json.dumps(payload, sort_keys=True).encode("utf-8")
        digest = hashlib.sha256(serialized).hexdigest()
        print(f"[CelsiusLeadDispatcher] Lead staged for {self.target_email} | SHA256={digest}")
        return digest


# ============================================================================
# 1. MOLECULAR ENGINEERING MODULE
# ============================================================================
class MolecularEngineering:
    """
    Module for Self-Assembled Monolayer (SAM) design and ONNX-based molecular property prediction.
    """
    def __init__(self, model_path="sam_design.onnx"):
        self.model_path = model_path
        self.session = None
        self.has_model = False

        if ort and os.path.exists(self.model_path):
            try:
                self.session = ort.InferenceSession(self.model_path, providers=["CPUExecutionProvider"])
                self.input_name = self.session.get_inputs()[0].name
                self.output_name = self.session.get_outputs()[0].name
                self.has_model = True
                print(f"[MolecularEngineering] ONNX model loaded: {self.model_path}")
            except Exception as e:
                print(f"[MolecularEngineering] Error loading model: {e}. Falling back to analytical heuristics.")
        else:
            print("[MolecularEngineering] Warning: ONNX model not found or onnxruntime not installed. Using analytical fallback heuristics.")

    def design_sam(self, desired_properties: dict) -> dict:
        if self.has_model and np is not None:
            input_data = np.array(
                [[desired_properties.get("contact_angle", 110.0),
                  desired_properties.get("binding_energy_ev", -0.8)]],
                dtype=np.float32
            )
            try:
                outputs = self.session.run([self.output_name], {self.input_name: input_data})
                return {
                    "predicted_molecule": "S-AlkaneThiol-ONNX-Optimized",
                    "predicted_packing_density": float(outputs[0][0][0]),
                    "method": "ONNX Model"
                }
            except Exception as e:
                print(f"[MolecularEngineering] Error during ONNX inference: {e}. Falling back to analytical heuristics.")
                return self.analytical_sam_design(desired_properties)
        return self.analytical_sam_design(desired_properties)

    def analytical_sam_design(self, desired_properties: dict) -> dict:
        contact_angle = desired_properties.get("contact_angle", 110.0)
        chain_length = int(max(6, min(18, (contact_angle - 70.0) / 3.0)))
        return {
            "suggested_structure": f"CH3(CH2){chain_length-1}SH",
            "chain_length": chain_length,
            "estimated_packing_density_molecules_nm2": 4.5,
            "method": "Analytical Heuristics"
        }


# ============================================================================
# 2. QUANTUM DOT SYNTHESIS MODULE
# ============================================================================
class QuantumDotSynthesis:
    """
    Module for predicting quantum dot synthesis parameters using the Brus equation.
    """
    def __init__(self):
        self.material_db = {
            "InP": {"bulk_bandgap_ev": 1.35, "electron_effective_mass": 0.08, "hole_effective_mass": 0.43, "dielectric_constant": 12.4},
            "CdSe": {"bulk_bandgap_ev": 1.74, "electron_effective_mass": 0.13, "hole_effective_mass": 0.45, "dielectric_constant": 10.6},
            "ZnS": {"bulk_bandgap_ev": 3.54, "electron_effective_mass": 0.28, "hole_effective_mass": 0.50, "dielectric_constant": 8.9}
        }
        self.electron_mass = 9.109e-31
        self.elementary_charge = 1.602e-19
        self.hbar = 1.05457e-34
        self.vacuum_permittivity = 8.854e-12

    def synthesize_qd_parameters(self, core_material="InP", shell_material="ZnS", target_emission_nm=520.0, temperature_k=300.0) -> dict:
        core_params = self.material_db.get(core_material)
        shell_params = self.material_db.get(shell_material)

        if not core_params:
            return {"error": f"Core material '{core_material}' parameters not found in database."}
        if not shell_params:
            return {"error": f"Shell material '{shell_material}' parameters not found in database."}

        effective_bulk_bandgap_ev = core_params["bulk_bandgap_ev"]
        target_bandgap_ev = (6.626e-34 * 3.0e8 / (target_emission_nm * 1e-9)) / self.elementary_charge
        required_confinement_ev = target_bandgap_ev - effective_bulk_bandgap_ev

        if required_confinement_ev <= 0:
            print(f"[QuantumDotSynthesis] Warning: Target emission wavelength ({target_emission_nm} nm) is at or below bulk bandgap ({effective_bulk_bandgap_ev} eV). Using minimum confinement energy of 0.01 eV.")
            required_confinement_ev = 0.01

        try:
            m_eff = core_params["electron_effective_mass"] * self.electron_mass
            required_radius_m = math.sqrt((self.hbar ** 2 * math.pi ** 2) / (2.0 * m_eff * (required_confinement_ev * self.elementary_charge)))
            required_radius_nm = required_radius_m * 1e9
        except ZeroDivisionError:
            print("[QuantumDotSynthesis] Error: Division by zero encountered during radius calculation. Check material parameters.")
            required_radius_nm = 10.0
        except Exception as e:
            print(f"[QuantumDotSynthesis] Unexpected error during radius calculation: {e}")
            required_radius_nm = 10.0

        shell_thickness_nm = max(0.5, min(2.0, required_radius_nm * 0.15))
        total_radius_nm = required_radius_nm + shell_thickness_nm

        return {
            "core_material": core_material,
            "shell_material": shell_material,
            "target_emission_nm": target_emission_nm,
            "calculated_core_radius_nm": round(required_radius_nm, 2),
            "estimated_shell_thickness_nm": round(shell_thickness_nm, 2),
            "total_radius_nm": round(total_radius_nm, 2),
            "calculated_confinement_energy_ev": round(required_confinement_ev, 3),
            "synthesis_temperature_k": temperature_k,
            "calculation_method": "Simplified Brus Equation & Heuristics"
        }


# ============================================================================
# 3. NANOTECHNOLOGY MODULE
# ============================================================================
class Nanotechnology:
    """
    Module for simulating and analyzing properties of nanomaterials like CNTs and Graphene.
    """
    def __init__(self, nano_model_path="nano_wiring.onnx"):
        self.nano_model_path = nano_model_path
        self.session = None
        self.has_model = False

        if ort and os.path.exists(self.nano_model_path):
            try:
                self.session = ort.InferenceSession(self.nano_model_path, providers=["CPUExecutionProvider"])
                self.input_name = self.session.get_inputs()[0].name
                self.output_name = self.session.get_outputs()[0].name
                self.has_model = True
                print(f"[Nanotechnology] ONNX model loaded: {self.nano_model_path}")
            except Exception as e:
                print(f"[Nanotechnology] Warning: Model loading failed ({e}). Falling back to analytical models.")
        else:
            print("[Nanotechnology] Warning: Model file not found. Using analytical models.")

    def analyze_ballistic_wiring(self, material_type: str, parameters: dict) -> dict:
        if self.has_model:
            return {
                "material": material_type,
                "analysis_method": "ONNX Precision (Simulated)"
            }

        if material_type == "CNT":
            chirality = parameters.get("chirality", (10, 10))
            length_nm = parameters.get("length_nm", 1000.0)
            is_metallic = (chirality[0] - chirality[1]) % 3 == 0
            resistivity_nm = 1e-4 if is_metallic else 1e-1
            resistance = resistivity_nm * length_nm
            ballistic_limit_A = 25e-6 if is_metallic else 1e-8

            return {
                "material": f"CNT {chirality}",
                "properties": {
                    "electrical_conductivity_S_per_m": 1e7 if is_metallic else 1e3,
                    "ballistic_limit_current_A": ballistic_limit_A,
                    "thermal_conductivity_W_per_mK": 3000,
                    "resistance_per_length_Ohm_per_nm": resistance / length_nm
                },
                "analysis_method": "Analytical (Simplified)"
            }
        elif material_type == "Graphene":
            layer_count = parameters.get("layer_count", 1)
            conductivity_base = 1e8 * layer_count
            current_density_limit = 1e8 * layer_count
            length_nm = parameters.get("length_nm", 1000.0)

            return {
                "material": f"Graphene (Layers: {layer_count})",
                "properties": {
                    "electrical_conductivity_S_per_m": conductivity_base,
                    "ballistic_limit_current_A": current_density_limit * (1e-9 * 1e-9),
                    "thermal_conductivity_W_per_mK": 5000 * layer_count,
                    "resistance_per_length_Ohm_per_nm": (1.0 / (conductivity_base * layer_count)) * (1e-9 * 1e-18)
                },
                "analysis_method": "Analytical (Simplified)"
            }
        else:
            return {"error": "Unsupported material type for Nanotechnology analysis."}


# ============================================================================
# 4. FIRMWARE ML PID MODULE (SHARED MEMORY INTEGRATION)
# ============================================================================
class FirmwareMLPID:
    """
    Handles C++ Firmware ML PID loop integration via shared memory communication.
    """
    def __init__(self, shm_name="VSSD_SHM_MEM"):
        self.shm_name = shm_name
        self.shm_size = 4096
        self.shm_handle = None
        self.self_shared_data_struct_size = 3300

    def initialize_shared_memory(self):
        if self.shm_handle:
            return True

        if sys.platform == "win32":
            try:
                self.shm_handle = mmap.mmap(-1, self.shm_size, tagname=self.shm_name)
                print(f"[Firmware ML PID] Successfully mapped shared memory tag '{self.shm_name}' on Windows.")
                return True
            except Exception as e:
                print(f"[Firmware ML PID] Error mapping shared memory on Windows: {e}")
                self.shm_handle = None
                return False
        else:
            if posix_ipc is None:
                print("[Firmware ML PID] posix_ipc module not found, shared memory unavailable on non-Windows.")
                return False
            try:
                try:
                    shm_obj = posix_ipc.SharedMemory(self.shm_name)
                    print(f"[Firmware ML PID] Connected to existing shared memory '{self.shm_name}'.")
                except posix_ipc.ExistError:
                    shm_obj = posix_ipc.SharedMemory(self.shm_name, posix_ipc.O_CREAT, size=self.shm_size)
                    print(f"[Firmware ML PID] Created new shared memory segment '{self.shm_name}' (Size: {self.shm_size} bytes).")

                self.shm_handle = mmap.mmap(shm_obj.fd, self.shm_size)
                shm_obj.close_fd()
                return True
            except Exception as e:
                print(f"[Firmware ML PID] Error initializing shared memory on POSIX: {e}")
                self.shm_handle = None
                return False

    def read_shared_state(self):
        if not self.shm_handle and not self.initialize_shared_memory():
            return None

        try:
            read_size = min(len(self.shm_handle), self.self_shared_data_struct_size)
            self.shm_handle.seek(0)
            header_size = 28
            if read_size < header_size:
                print("[Firmware ML PID] Error: Not enough data to read header.")
                return None

            raw_header = self.shm_handle.read(header_size)
            if np is not None:
                header_fmt = np.dtype([("kp", "f4"), ("ki", "f4"), ("kd", "f4"), ("head", "i4"), ("tail", "i4"), ("us", "i8")])
                header_data = np.frombuffer(raw_header, dtype=header_fmt)[0]
                kp, ki, kd = float(header_data["kp"]), float(header_data["ki"]), float(header_data["kd"])
                head, tail, us = int(header_data["head"]), int(header_data["tail"]), int(header_data["us"])
            else:
                import struct
                kp, ki, kd, head, tail, us = struct.unpack("<fffiiq", raw_header)

            return {
                "kp": kp,
                "ki": ki,
                "kd": kd,
                "head": head,
                "tail": tail,
                "current_us_ms": us,
                "queue_status": f"Head: {head}, Tail: {tail}"
            }
        except Exception as e:
            print(f"[Firmware ML PID] Error reading shared memory: {e}")
            return None

    def write_shared_state(self, updated_data: dict):
        if not self.shm_handle and not self.initialize_shared_memory():
            return False

        try:
            kp = float(updated_data.get("kp", 0.0))
            ki = float(updated_data.get("ki", 0.0))
            kd = float(updated_data.get("kd", 0.0))

            self.shm_handle.seek(0)
            if np is not None:
                header_pack = np.array([(kp, ki, kd)], dtype=[("kp", "f4"), ("ki", "f4"), ("kd", "f4")])
                self.shm_handle.write(header_pack.tobytes())
            else:
                import struct
                self.shm_handle.write(struct.pack("<fff", kp, ki, kd))

            event = updated_data.get("telemetry_event")
            if event:
                current_queue_size = 0
                max_queue_size = 100
                if current_queue_size < max_queue_size:
                    if np is not None:
                        event_bg = np.array([max_queue_size - current_queue_size], dtype=np.int32)
                        self.shm_handle.write(event_bg.tobytes())
                    else:
                        import struct
                        self.shm_handle.write(struct.pack("<i", max_queue_size - current_queue_size))

            self.shm_handle.flush()
            return True
        except Exception as e:
            print(f"[Firmware ML PID] Error writing shared memory: {e}")
            return False

    def run_shm_loop_step(self):
        shared_state = self.read_shared_state()
        if shared_state is None:
            print("[Firmware ML PID] Warning: Failed to read state from shared memory. Skipping step.")
            return

        current_timestamp_us = time.time_ns() // 1000
        print(f"[Firmware ML PID] Read SHM state at {current_timestamp_us} us:")
        print(f"  Gains: [Kp={shared_state['kp']:.2f}, Ki={shared_state['ki']:.2f}, Kd={shared_state['kd']:.2f}]")

        new_kp = shared_state["kp"] * 1.01
        new_ki = shared_state["ki"] * 1.005
        new_kd = shared_state["kd"] * 0.99

        simulated_raw_pv = shared_state.get("kp", 1.0) * 10.0
        simulated_filtered_pv = simulated_raw_pv * 0.98
        simulated_setpoint = 100.0
        simulated_control_output = new_kp * 0.5

        telemetry_event = {
            "timestamp_us": current_timestamp_us,
            "filtered_pv": simulated_filtered_pv,
            "new_kp": new_kp,
            "new_ki": new_ki,
            "new_kd": new_kd,
            "setpoint": simulated_setpoint,
            "control_output": simulated_control_output,
            "tail_drop_count": 0
        }

        update_shm_data = {
            "kp": new_kp,
            "ki": new_ki,
            "kd": new_kd,
            "head": shared_state["head"] + 1,
            "tail": shared_state["tail"],
            "timestamp_us": current_timestamp_us // 1000,
            "event_queue": [telemetry_event]
        }

        if update_shm_data.get("head", 0) > 2:
            print("[Firmware ML PID] Body Mixer calling condition met (simulated), logic acquire trigger sent to C++.")


# ============================================================================
# 5. BUDGET ANALYSIS MODULE
# ============================================================================
class BudgetAnalysis:
    """
    Analyzes costs associated with R&D and manufacturing, including specialized
    cleanroom and high-purity material costs.
    """
    def __init__(self, config_file="budget_config.json"):
        self.config_file = config_file
        self.self_costs = {
            "cleanroom_cost_per_hr_usd": 500.0,
            "process_time_hrs": 0.0,
            "material_usage_kg": {},
            "material_cost_per_kg": {
                "In_99.999%": 1500.0,
                "Cd_Selenide": 1800.0,
                "Ga_Phosphide": 2000.0,
                "Si_4N": 1200.0,
                "Zn_Sulfur": 1000.0,
                "Graphene_Precursor": 8000.0,
                "CNT_Precursor": 5000.0,
                "SAM_Precursor": 12000.0
            },
            "personnel_cost_per_hr_usd": 150.0,
            "personnel_hrs": 0.0,
            "equipment_amortization_per_hr_usd": 50.0,
            "waste_disposal_per_kg_usd": 20.0
        }
        self.load_config(config_file)

    def load_config(self, config_file):
        if os.path.exists(config_file):
            try:
                with open(config_file, "r") as f:
                    loaded_config = json.load(f)
                    for key, value in loaded_config.items():
                        if key in self.self_costs:
                            if isinstance(value, dict) and isinstance(self.self_costs[key], dict):
                                self.self_costs[key].update(value)
                            else:
                                self.self_costs[key] = value
                print(f"[Budget Analysis] Loaded configuration from {config_file}")
            except Exception as e:
                print(f"[Budget Analysis] Warning: Error loading config ({config_file}): {e}. Using defaults.")
        else:
            print(f"[Budget Analysis] Configuration file '{config_file}' not found. Using default cost structure.")

    def set_process_parameters(self, process_time_hrs: float, material_usage_kg: dict, personnel_hrs: float):
        self.self_costs["process_time_hrs"] = float(process_time_hrs)
        self.self_costs["material_usage_kg"] = material_usage_kg
        self.self_costs["personnel_hrs"] = float(personnel_hrs)
        print(f"[Budget Analysis] Process parameters set: Time={process_time_hrs}hrs, Personnel={personnel_hrs}hrs.")

    def calculate_project_cost(self) -> float:
        total_cost = 0.0
        cleanroom_cost = self.self_costs["cleanroom_cost_per_hr_usd"] * self.self_costs["process_time_hrs"]
        total_cost += cleanroom_cost

        equipment_cost = self.self_costs["equipment_amortization_per_hr_usd"] * self.self_costs["process_time_hrs"]
        total_cost += equipment_cost

        material_cost = 0.0
        used_materials = self.self_costs.get("material_usage_kg", {})
        material_prices = self.self_costs.get("material_cost_per_kg", {})
        for material, usage in used_materials.items():
            price_per_kg = material_prices.get(material)
            if price_per_kg is not None:
                material_cost += usage * price_per_kg
            else:
                print(f"[Budget Analysis] Warning: Price for material '{material}' not found. Skipping.")
        total_cost += material_cost

        personnel_cost = self.self_costs["personnel_cost_per_hr_usd"] * self.self_costs["personnel_hrs"]
        total_cost += personnel_cost

        waste_disposal_cost = 0.0
        for material, usage in used_materials.items():
            waste_disposal_cost += usage * self.self_costs["waste_disposal_per_kg_usd"]
        total_cost += waste_disposal_cost

        print(f"[Budget Analysis] Cost Breakdown (Estimated): Cleanroom/Equip=${cleanroom_cost + equipment_cost:.2f}, Materials=${material_cost:.2f}, Personnel=${personnel_cost:.2f}, Waste=${waste_disposal_cost:.2f}")
        return round(total_cost, 2)


# ============================================================================
# 6. MAIN ENGINE CLASS & INTERACTIVE COMMAND PROMPT
# ============================================================================
class VSSDUltraEngine:
    """
    The central orchestrator of the VSSD Ultra Engine.
    """
    def __init__(self):
        self.pid_shm_name = "VSSD_SHM_MEM"
        self.sam_model_path = "sam_design.onnx"
        self.nano_model_path = "nano_wiring.onnx"
        self.budget_config = "budget_config.json"

        self.learning_rate = 0.001
        self.anti_gravity_bias = 2.2470114
        self.ecu_throttle_limit = 100.0

        self.pid_firmware = FirmwareMLPID(self.pid_shm_name)
        self.molecular_engineer = MolecularEngineering(self.sam_model_path)
        self.quantum_synthesizer = QuantumDotSynthesis()
        self.nanotech_analyst = Nanotechnology(self.nano_model_path)
        self.budget_analyst = BudgetAnalysis(self.budget_config)
        self.governance_guard = SovereignGovernanceGuard()
        self.ocular_diagnostics = OcularDiagnosticEngine(hard_kill_enabled=False)
        self.lead_dispatcher = CelsiusLeadDispatcher("info@celsiusmediagroup.co.za")

        print("=" * 60)
        print("VSSD ULTRA ENGINE - INTEGRATED NANOTECHNOLOGY PLATFORM")
        print("=" * 60)
        print("[NOTIFICATION] All modules initialized successfully.")
        print("=" * 60)

    def compute_anti_gravity_throttling(self, mass_kg: float, target_lift_force: float) -> dict:
        required_power_kw = (mass_kg * 9.80665 * self.anti_gravity_bias) / 1000.0
        optimized_throttle = min(100.0, max(0.0, (target_lift_force / (mass_kg * 9.80665)) * 100.0 * (100.0 / self.ecu_throttle_limit)))
        power_efficiency_gain = 100.0 - (optimized_throttle * self.anti_gravity_bias)

        return {
            "required_power_kw": round(float(required_power_kw), 2),
            "throttle_percent": round(float(optimized_throttle), 2),
            "power_efficiency_gain": round(float(power_efficiency_gain), 2)
        }

    def solve_physics_query(self, category: str, query: str) -> str:
        query_lower = query.lower()

        if "quantum dot" in query_lower or "emitter" in query_lower:
            qd_params = self.quantum_synthesizer.synthesize_qd_parameters()
            return json.dumps(qd_params, indent=2)

        if "nanowire" in query_lower or "graphene" in query_lower or "cnt" in query_lower:
            nano_analysis = self.nanotech_analyst.analyze_ballistic_wiring("CNT", {"chirality": (10, 10), "length_nm": 500.0})
            return json.dumps(nano_analysis, indent=2)

        if "sam" in query_lower or "surface" in query_lower:
            sam_design = self.molecular_engineer.design_sam({"contact_angle": 115.0})
            return json.dumps(sam_design, indent=2)

        return "ONNX Physics Core processed query. Specific module not triggered."

    def interactive_prompt(self):
        print("=" * 60)
        print("VSSD ULTRA ENGINE - INTERACTIVE COMMAND CONSOLE")
        print("=" * 60)
        print("Type 'help' for available commands.")

        while True:
            try:
                cmd = input("vssd-engine> ").strip()
                if not cmd:
                    continue

                if cmd.lower() in ["exit", "quit", "shutdown"]:
                    print("Shutting down VSSD Engine...")
                    break

                elif cmd.lower() == "help":
                    print("\nAvailable Commands:")
                    print("  budget - Run a sample budget analysis")
                    print("  physics <query> - Query the physics simulation engine (e.g., 'physics quantum dot emitter')")
                    print("  pid_step - Execute one step of the C++ PID loop via shared memory")
                    print("  ag_throttle - Compute anti-gravity throttling parameters")
                    print("  audit_ugpe <payload_mb> <delta_t_sec> - Check UGPE Delta_S against D_crit=6.66")
                    print("  ocular_sweep - Run Shoshin -> Zanshin -> Black Zetsu -> Mushin check")
                    print("  help - Display this help message")
                    print("  exit, quit, shutdown - Terminate the engine")
                    print("=" * 20)

                elif cmd.lower() == "budget":
                    print("\n[BUDGET ANALYSIS]")
                    self.budget_analyst.set_process_parameters(
                        process_time_hrs=15.0,
                        material_usage_kg={"Si_4N": 0.5, "CNT_Precursor": 0.002, "SAM_Precursor": 0.001},
                        personnel_hrs=40.0
                    )
                    cost_report = self.budget_analyst.calculate_project_cost()
                    print(f"\n[BUDGET REPORT] Total Estimated Project Cost: ${cost_report}\n")

                elif cmd.startswith("physics"):
                    query_text = cmd[len("physics"):].strip()
                    if not query_text:
                        print("Please provide a query for the physics engine (e.g., 'physics quantum dot').")
                    else:
                        print(f"\n[PHYSICS QUERY] Processing: '{query_text}'")
                        result = self.solve_physics_query("Physics", query_text)
                        print(f"\n[PHYSICS RESULT]\n{result}\n")

                elif cmd.lower() == "pid_step":
                    print("\n[FIRMWARE SIMULATION]")
                    self.pid_firmware.run_shm_loop_step()
                    print("[PID] Step simulation complete.\n")

                elif cmd.startswith("ag_throttle"):
                    parts = cmd.split()
                    if len(parts) == 3:
                        try:
                            mass = float(parts[1])
                            lift = float(parts[2])
                            print(f"\n[ANTI-GRAVITY CALCULATION] Mass={mass}kg, Target Lift={lift}N")
                            throttle_result = self.compute_anti_gravity_throttling(mass, lift)
                            print(f"[THROTTLE RESULT]\n{json.dumps(throttle_result, indent=2)}\n")
                        except ValueError:
                            print("Invalid arguments for ag_throttle. Please provide numeric values for mass and lift force.")
                    else:
                        print("Usage: ag_throttle <mass_kg> <lift_force_N>")

                elif cmd.startswith("audit_ugpe"):
                    parts = cmd.split()
                    payload_mb = float(parts[1]) if len(parts) > 1 else 250.0
                    delta_t = float(parts[2]) if len(parts) > 2 else 25.0
                    res = self.governance_guard.evaluate_ugpe_entropy(payload_mb, delta_t)
                    print(f"\n[UGPE GOVERNANCE AUDIT]\n{json.dumps(res, indent=2)}\n")

                elif cmd.lower() == "ocular_sweep":
                    res = self.ocular_diagnostics.run_full_ocular_sweep(0.20, 0.30, 0.40, 0.05)
                    print(f"\n[OCULAR SWEEP]\n{json.dumps(res, indent=2)}\n")

                else:
                    print(f"\nUnknown command: '{cmd}'. Type 'help' for available commands.")

            except KeyboardInterrupt:
                print("\nOperation interrupted by user.")
                break
            except Exception as e:
                print(f"[ERROR] An unexpected error occurred: {e}")


if __name__ == "__main__":
    engine = VSSDUltraEngine()
    engine.interactive_prompt()
