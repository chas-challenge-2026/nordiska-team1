"""Synthetic Banking Data Generator for Nordiska Sparbanken.

Generates deterministic, highly realistic synthetic customers, savings accounts,
and transactional ledger entries matching Swedish retail banking domains,
accounting invariants, day-count interest calculations, and preliminary tax rules.
"""

from __future__ import annotations

import base64
import hashlib
import math
import os
import random
import struct
from dataclasses import dataclass
from datetime import date, datetime, timedelta, timezone
from typing import List, Tuple

FIRST_NAMES = [
    "Anna", "Erik", "Karin", "Johan", "Maria", "Karl", "Sara", "Anders",
    "Emma", "Mikael", "Astrid", "Per", "Elin", "Lars", "Linnea", "Fredrik",
    "Ida", "Gustav", "Maja", "Daniel", "Sofia", "Magnus", "Hanna", "Oskar",
    "Clara", "Niklas", "Ebba", "Alexander", "Agnes", "Stefan", "Ingrid",
    "Henrik", "Alma", "Viktor", "Svea", "Emil", "Signe", "Christian",
    "Alva", "Andreas", "Lovisa", "Jonas", "Frida", "Marcus", "Klara",
    "Filip", "Matilda", "Simon"
]

LAST_NAMES = [
    "Andersson", "Johansson", "Karlsson", "Nilsson", "Eriksson", "Larsson",
    "Olsson", "Persson", "Svensson", "Gustafsson", "Pettersson", "Jonsson",
    "Jansson", "Hansson", "Bengtsson", "Jönsson", "Lindqvist", "Lindgren",
    "Bergström", "Axelsson", "Lundberg", "Forsberg", "Sandberg", "Ekström",
    "Hedlund", "Holm", "Nyström", "Öberg", "Söderberg", "Nordström", "Lundqvist"
]

COMPANIES = [
    "Volvo Group AB", "Ericsson AB", "Spotify AB", "H&M Hennes & Mauritz",
    "Scania AB", "Region Stockholm", "Region Skåne", "Göteborgs Stad",
    "Klarna Bank AB", "IKEA AB", "SEB Group", "Nordea Bank Abp", "SVT AB"
]

MERCHANTS_GROCERY = [
    "ICA Supermarket", "ICA Kvantum", "ICA Maxi", "Coop Konsum",
    "Stora Coop", "Hemköp City", "Willys", "Lidl", "City Gross"
]

MERCHANTS_RETAIL = [
    "Systembolaget", "Apoteket Hjärtat", "Kronans Apotek", "Pressbyrån",
    "7-Eleven", "Espresso House", "Clas Ohlson", "IKEA Barkarby",
    "Elgiganten", "XXL Sport", "Kjell & Company", "Stadium"
]

MERCHANTS_COMMUTE = [
    "SL Spärr Stockholm", "SL Månadskort", "SJ Regionaltåg", "Västtrafik",
    "Skånetrafiken", "Circle K Drivmedel", "OKQ8 Bensin", "Preem Station"
]

BILLS_AND_SERVICES = [
    "Hyra Heimstaden", "Hyra Wallenstam", "Ellevio AB Elnät", "Vattenfall Kund",
    "Telia Sverige", "Tele2 Mobil", "If Skadeförsäkring", "Trygg-Hansa",
    "Klarna Månadsfaktura", "Spotify Prenumeration", "Netflix", "SATS Gymkort"
]

ACCOUNT_CONFIGS = [
    {"type": "flex", "name": "Sparkonto Flex", "rate": 0.035000},
    {"type": "fix", "name": "Fasträntekonto Fix", "rate": 0.041000},
    {"type": "standard", "name": "Standard Sparkonto", "rate": 0.025000},
    {"type": "saving", "name": "Högräntekonto Förmån", "rate": 0.035000},
    {"type": "premium", "name": "Premium Sparkonto", "rate": 0.040000},
]

VOWELS = ["a", "e", "i", "o", "u", "y", "å", "ä", "ö"]
CONSONANTS = ["b", "d", "f", "g", "h", "j", "k", "l", "m", "n", "p", "r", "s", "t", "v"]
SYMBOLS_BANKING = ["-", "/", ".", "*", "&", "#"]


def stable_seed(master_seed: int, index: int) -> int:
    """Create a deterministic 64-bit seed from a master seed and index."""
    data = f"{master_seed}:{index}".encode("ascii")
    return int.from_bytes(hashlib.sha256(data).digest()[:8], "big")


def luhn_checksum(digits: str) -> int:
    """Calculate the Luhn modulo-10 check digit."""
    total = 0
    for i, char in enumerate(digits):
        d = int(char)
        if i % 2 == 0:
            d *= 2
        if d > 9:
            d -= 9
        total += d
    return (10 - (total % 10)) % 10


def generate_personnummer(rng: random.Random) -> str:
    """Generate a realistic Swedish 12-digit Personal Identity Number with valid Luhn check.
    
    Format: YYYYMMDDNNNC (12 digits)
    """
    year = rng.randint(1930, 2007)
    month = rng.randint(1, 12)
    max_days = 28 if month == 2 else (30 if month in (4, 6, 9, 11) else 31)
    day = rng.randint(1, max_days)
    individual = rng.randint(100, 999)
    luhn_part = f"{year % 100:02d}{month:02d}{day:02d}{individual:03d}"
    check = luhn_checksum(luhn_part)
    return f"{year:04d}{month:02d}{day:02d}{individual:03d}{check}"


def generate_aspnet_identity_password_hash(password: str = "password123") -> str:
    """Generate a standard ASP.NET Core Identity PasswordHasher v3 PBKDF2 hash.
    
    Format: 0x01 | Prf (HMACSHA256) | IterationCount (10000) | SaltLength (16) | Salt | Subkey (32)
    """
    salt = os.urandom(16)
    iterations = 10000
    prf = 1  # HMACSHA256
    subkey = hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), salt, iterations, dklen=32)
    header = struct.pack(">BIII", 1, prf, iterations, len(salt))
    return base64.b64encode(header + salt + subkey).decode("ascii")


COMMON_PASSWORD_HASH = generate_aspnet_identity_password_hash("password123")


def pareto_sample(rng: random.Random, x_min: float, alpha: float, x_max: float) -> int:
    """Draw a sample from a bounded Pareto distribution."""
    u = rng.random()
    val = x_min / ((1.0 - u) ** (1.0 / alpha))
    return int(min(x_max, max(x_min, math.floor(val))))


def generate_varied_word(rng: random.Random, min_len: int = 3, max_len: int = 8) -> str:
    """Generate a word with realistic Swedish character frequencies."""
    target_len = rng.randint(min_len, max_len)
    chars = []
    start_with_consonant = rng.random() < 0.7
    for i in range(target_len):
        if (i % 2 == 0) == start_with_consonant:
            chars.append(rng.choice(CONSONANTS))
        else:
            chars.append(rng.choice(VOWELS))
    word = "".join(chars)
    if rng.random() < 0.4:
        word = word.capitalize()
    return word


def generate_stress_label(rng: random.Random, tx_type: str) -> str:
    """Generate varied-length descriptions with Swedish characters and banking symbols."""
    roll = rng.random()
    if roll < 0.05:
        # Long tail (up to 120+ chars)
        words = [generate_varied_word(rng, 4, 7) for _ in range(rng.randint(12, 18))]
        return f"Överföring {rng.choice(SYMBOLS_BANKING)} " + " ".join(words)
    elif roll < 0.20:
        words = [generate_varied_word(rng, 3, 6) for _ in range(rng.randint(6, 9))]
        return " ".join(words) + f" {rng.randint(100, 9999)}/{rng.choice(['SE', 'AB', 'KB'])}"
    else:
        words = [generate_varied_word(rng, 4, 7) for _ in range(rng.randint(2, 4))]
        prefix = "Kortköp " if tx_type == "withdrawal" else "Överföring "
        return prefix + " ".join(words)


def generate_realistic_label(rng: random.Random, tx_type: str) -> str:
    """Generate a realistic 'Action @ Place' Swedish transaction label."""
    if tx_type == "deposit":
        roll = rng.random()
        if roll < 0.40:
            return f"Lön från {rng.choice(COMPANIES)}"
        elif roll < 0.70:
            return f"Swish från {rng.choice(FIRST_NAMES)}"
        elif roll < 0.85:
            return "Överföring från sparkonto"
        elif roll < 0.95:
            return rng.choice(["Skatteåterbäring Skatteverket", "Barnbidrag Försäkringskassan"])
        else:
            return f"Insättning Bankomat {rng.choice(['Odenplan', 'Sergels Torg', 'Centralen', 'Avenyn'])}"
    else:
        roll = rng.random()
        if roll < 0.45:
            return f"Kortköp på {rng.choice(MERCHANTS_GROCERY)}"
        elif roll < 0.70:
            return f"Kortköp på {rng.choice(MERCHANTS_RETAIL)}"
        elif roll < 0.85:
            return f"Swish till {rng.choice(FIRST_NAMES)}"
        elif roll < 0.95:
            return f"Autogiro {rng.choice(BILLS_AND_SERVICES)}"
        else:
            return f"Resa {rng.choice(MERCHANTS_COMMUTE)}"


@dataclass
class SyntheticCustomer:
    id: int
    personal_num: str
    name: str
    email: str
    phone_number: str
    password_hash: str
    created_at: datetime


@dataclass
class SyntheticAccount:
    id: int
    customer_id: int
    account_number: str
    account_name: str
    account_type: str
    balance: float
    interest_rate: float
    status: str
    created_at: datetime


@dataclass
class SyntheticLedgerEntry:
    id: int
    account_id: int
    entry_type: str
    amount: float
    label: str
    created_at: datetime


class SyntheticDatasetGenerator:
    """Deterministic generator for synthetic retail banking customers and accounts."""

    def __init__(self, master_seed: int = 42, year: int = 2026, stress_text: bool = False):
        self.master_seed = master_seed
        self.year = year
        self.stress_text = stress_text

    def generate(
        self,
        customer_count: int,
        start_customer_id: int = 1000,
        start_account_id: int = 10000,
        start_ledger_id: int = 100000,
        existing_personal_nums: Optional[set[str]] = None,
    ) -> Tuple[List[SyntheticCustomer], List[SyntheticAccount], List[SyntheticLedgerEntry]]:
        """Generate customers, accounts, and ledger entries."""
        customers: List[SyntheticCustomer] = []
        accounts: List[SyntheticAccount] = []
        ledger_entries: List[SyntheticLedgerEntry] = []

        used_personal_nums: set[str] = set(existing_personal_nums) if existing_personal_nums else set()

        curr_account_id = start_account_id
        curr_ledger_id = start_ledger_id

        first_day = date(self.year, 1, 1)
        last_day = date(self.year, 12, 31)
        days_in_year = (last_day - first_day).days + 1
        is_leap = (self.year % 4 == 0 and self.year % 100 != 0) or (self.year % 400 == 0)
        days_divisor = 366.0 if is_leap else 365.0

        for idx in range(customer_count):
            cust_id = start_customer_id + idx
            rng = random.Random(stable_seed(self.master_seed, cust_id))

            first_name = rng.choice(FIRST_NAMES)
            last_name = rng.choice(LAST_NAMES)
            full_name = f"{first_name} {last_name}"
            person_num = generate_personnummer(rng)
            while person_num in used_personal_nums:
                person_num = generate_personnummer(rng)
            used_personal_nums.add(person_num)
            email = f"{first_name.lower()}.{last_name.lower()}{cust_id}@nordiska-demo.se"
            phone = f"+4670{rng.randint(1000000, 9999999)}"
            cust_created = datetime(self.year, 1, 1, 8, 0, 0, tzinfo=timezone.utc)

            customer = SyntheticCustomer(
                id=cust_id,
                personal_num=person_num,
                name=full_name,
                email=email,
                phone_number=phone,
                password_hash=COMMON_PASSWORD_HASH,
                created_at=cust_created,
            )
            customers.append(customer)

            acct_count = pareto_sample(rng, x_min=1.0, alpha=1.9, x_max=6.0)

            for acct_idx in range(acct_count):
                account_id = curr_account_id
                curr_account_id += 1

                cfg = ACCOUNT_CONFIGS[acct_idx % len(ACCOUNT_CONFIGS)]
                account_number = f"NOR-{account_id:06d}"
                account_type = cfg["type"]
                account_name = cfg["name"]
                interest_rate = cfg["rate"]

                tx_count = pareto_sample(rng, x_min=6.0, alpha=1.45, x_max=400.0)
                balance = 0.0

                opening_amount = float(rng.randint(15000, 180000))
                balance += opening_amount
                tx_label = (
                    generate_stress_label(rng, "deposit")
                    if self.stress_text
                    else "Öppningsinsättning"
                )

                ledger_entries.append(
                    SyntheticLedgerEntry(
                        id=curr_ledger_id,
                        account_id=account_id,
                        entry_type="deposit",
                        amount=opening_amount,
                        label=tx_label,
                        created_at=datetime(self.year, 1, 1, 9, 0, 0, tzinfo=timezone.utc),
                    )
                )
                curr_ledger_id += 1

                customer_events: dict[date, list[tuple[str, float, str]]] = {}
                event_offsets = sorted(
                    [rng.randint(0, days_in_year - 1) for _ in range(tx_count)]
                )

                for offset in event_offsets:
                    event_date = first_day + timedelta(days=offset)
                    is_deposit = (balance < 5000.0) or (rng.random() < 0.45)

                    if is_deposit:
                        amt = round(float(rng.randint(200, 35000)) + rng.random(), 2)
                        lbl = (
                            generate_stress_label(rng, "deposit")
                            if self.stress_text
                            else generate_realistic_label(rng, "deposit")
                        )
                        customer_events.setdefault(event_date, []).append(("deposit", amt, lbl))
                        balance += amt
                    else:
                        max_withdrawal = max(100.0, balance * 0.7)
                        amt = round(
                            float(rng.randint(50, int(min(20000.0, max_withdrawal))))
                            + rng.random(),
                            2,
                        )
                        lbl = (
                            generate_stress_label(rng, "withdrawal")
                            if self.stress_text
                            else generate_realistic_label(rng, "withdrawal")
                        )
                        customer_events.setdefault(event_date, []).append(("withdrawal", -amt, lbl))
                        balance -= amt

                sim_balance = opening_amount
                sim_accrued = 0.0
                current_day = first_day

                while current_day <= last_day:
                    for tx_type, amount, label in customer_events.get(current_day, []):
                        sim_balance += amount
                        event_time = datetime(
                            current_day.year,
                            current_day.month,
                            current_day.day,
                            rng.randint(8, 20),
                            rng.randint(0, 59),
                            rng.randint(0, 59),
                            tzinfo=timezone.utc,
                        )
                        ledger_entries.append(
                            SyntheticLedgerEntry(
                                id=curr_ledger_id,
                                account_id=account_id,
                                entry_type=tx_type,
                                amount=amount,
                                label=label,
                                created_at=event_time,
                            )
                        )
                        curr_ledger_id += 1

                    if sim_balance > 0 and interest_rate > 0:
                        daily_interest = (sim_balance * interest_rate) / days_divisor
                        sim_accrued += daily_interest

                    next_day = current_day + timedelta(days=1)
                    if next_day.month != current_day.month or current_day == last_day:
                        gross_credit = round(sim_accrued, 2)
                        sim_accrued = 0.0

                        if gross_credit >= 0.01:
                            tax_withheld = round(gross_credit * 0.30, 2)
                            month_end_time = datetime(
                                current_day.year,
                                current_day.month,
                                current_day.day,
                                23,
                                59,
                                50,
                                tzinfo=timezone.utc,
                            )
                            ledger_entries.append(
                                SyntheticLedgerEntry(
                                    id=curr_ledger_id,
                                    account_id=account_id,
                                    entry_type="interest",
                                    amount=gross_credit,
                                    label="Ränteutbetalning",
                                    created_at=month_end_time,
                                )
                            )
                            curr_ledger_id += 1

                            ledger_entries.append(
                                SyntheticLedgerEntry(
                                    id=curr_ledger_id,
                                    account_id=account_id,
                                    entry_type="tax",
                                    amount=-tax_withheld,
                                    label="Preliminärskatt 30%",
                                    created_at=month_end_time + timedelta(seconds=5),
                                )
                            )
                            curr_ledger_id += 1

                            sim_balance += gross_credit - tax_withheld

                    current_day = next_day

                account = SyntheticAccount(
                    id=account_id,
                    customer_id=cust_id,
                    account_number=account_number,
                    account_name=account_name,
                    account_type=account_type,
                    balance=round(sim_balance, 2),
                    interest_rate=interest_rate,
                    status="active",
                    created_at=datetime(self.year, 1, 1, 8, 30, 0, tzinfo=timezone.utc),
                )
                accounts.append(account)

        return customers, accounts, ledger_entries
