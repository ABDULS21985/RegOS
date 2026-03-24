from __future__ import annotations

from pathlib import Path
from lxml import etree


XS_NS = "http://www.w3.org/2001/XMLSchema"
DMB_NS = "urn:centralbank:dmb:reporting:v1"
XSI_NS = "http://www.w3.org/2001/XMLSchema-instance"
NSMAP = {"xs": XS_NS}
XS = f"{{{XS_NS}}}"

RETURN_SECTIONS = {
    "CAR": "CapitalAdequacy",
    "LCR": "LiquidityCoverage",
    "NSFR": "StableFunding",
    "CRR": "CreditRisk",
    "MRR": "MarketRisk",
    "OPR": "OperationalRisk",
    "LEX": "LargeExposures",
    "AQR": "AssetQuality",
    "FIN": "FinancialStatements",
    "AML": "AMLCompliance",
    "FXP": "FXPosition",
    "DSR": "DepositStructure",
    "STR": "StressTesting",
    "GOV": "Governance",
}

FILE_TEMPLATE = "DMB_{return_code}_Valid_Sample.xml"
DEFAULT_DATE = "2026-03-31"
DEFAULT_START_DATE = "2026-01-01"
DEFAULT_DATETIME = "2026-04-05T10:00:00Z"


def local_name(qname: str | None) -> str | None:
    if not qname:
        return None
    return qname.split(":", 1)[-1]


class DmbSampleGenerator:
    def __init__(self, xsd_path: Path, output_dir: Path) -> None:
        self.xsd_path = xsd_path
        self.output_dir = output_dir
        self.schema_doc = etree.parse(str(xsd_path))
        self.schema = etree.XMLSchema(self.schema_doc)
        self.root = self.schema_doc.getroot()
        self.complex_types = {
            node.get("name"): node
            for node in self.root.findall(f"{XS}complexType")
            if node.get("name")
        }
        self.simple_types = {
            node.get("name"): node
            for node in self.root.findall(f"{XS}simpleType")
            if node.get("name")
        }

    def generate_all(self) -> list[Path]:
        paths: list[Path] = []
        for return_code, section_name in RETURN_SECTIONS.items():
            xml_doc = self.build_document(return_code, section_name)
            out_path = self.output_dir / FILE_TEMPLATE.format(return_code=return_code)
            out_path.write_bytes(
                etree.tostring(
                    xml_doc,
                    xml_declaration=True,
                    encoding="UTF-8",
                    pretty_print=True,
                )
            )
            self.validate(out_path)
            paths.append(out_path)
        return paths

    def build_document(self, return_code: str, section_name: str) -> etree._ElementTree:
        root = etree.Element(
            etree.QName(DMB_NS, "DMBReturn"),
            nsmap={"dmb": DMB_NS, "xsi": XSI_NS},
        )
        root.set(etree.QName(XSI_NS, "schemaLocation"), f"{DMB_NS} {self.xsd_path.name}")
        root.set("schemaVersion", "1.0")

        root.append(self.build_named_element("Header", "HeaderType", return_code))
        root.append(self.build_named_element(section_name, f"{section_name}Type", return_code))
        root.append(self.build_named_element("Signature", "SignatureType", return_code))

        return etree.ElementTree(root)

    def build_named_element(
        self,
        element_name: str,
        type_name: str | None,
        return_code: str,
        *,
        element_decl: etree._Element | None = None,
    ) -> etree._Element:
        element = etree.Element(etree.QName(DMB_NS, element_name))

        if type_name:
            complex_type = self.complex_types.get(local_name(type_name))
            simple_type = self.simple_types.get(local_name(type_name))
            if complex_type is not None:
                self.populate_complex(element, complex_type, return_code)
                return element
            if simple_type is not None:
                element.text = self.sample_for_type(simple_type, element_name, return_code)
                return element
            element.text = self.sample_for_builtin(local_name(type_name), element_name, return_code)
            return element

        if element_decl is not None:
            complex_type = element_decl.find(f"{XS}complexType")
            simple_type = element_decl.find(f"{XS}simpleType")
            if complex_type is not None:
                self.populate_complex(element, complex_type, return_code)
                return element
            if simple_type is not None:
                element.text = self.sample_for_type(simple_type, element_name, return_code)
                return element

        element.text = self.sample_for_builtin("string", element_name, return_code)
        return element

    def populate_complex(
        self,
        element: etree._Element,
        complex_type: etree._Element,
        return_code: str,
    ) -> None:
        for attr in complex_type.findall(f"{XS}attribute"):
            if attr.get("use") != "required" and attr.get("fixed") is None:
                continue
            attr_name = attr.get("name")
            if not attr_name:
                continue
            if attr.get("fixed") is not None:
                element.set(attr_name, attr.get("fixed"))
                continue
            attr_type = attr.get("type")
            element.set(attr_name, self.sample_for_type_name(attr_type, attr_name, return_code))

        sequence = complex_type.find(f"{XS}sequence")
        if sequence is None:
            return

        for child_decl in sequence.findall(f"{XS}element"):
            if child_decl.get("minOccurs") == "0":
                continue
            repeats = 1
            if child_decl.get("maxOccurs") == "unbounded":
                repeats = 1
            for _ in range(repeats):
                child_name = child_decl.get("name")
                if not child_name:
                    continue
                child_type = child_decl.get("type")
                child = self.build_named_element(
                    child_name,
                    local_name(child_type) if child_type else None,
                    return_code,
                    element_decl=child_decl,
                )
                element.append(child)

    def sample_for_type_name(self, type_name: str | None, element_name: str, return_code: str) -> str:
        local = local_name(type_name)
        if local in self.simple_types:
            return self.sample_for_type(self.simple_types[local], element_name, return_code)
        return self.sample_for_builtin(local, element_name, return_code)

    def sample_for_type(self, simple_type: etree._Element, element_name: str, return_code: str) -> str:
        restriction = simple_type.find(f"{XS}restriction")
        if restriction is None:
            return self.sample_for_builtin(simple_type.get("name"), element_name, return_code)

        enums = restriction.findall(f"{XS}enumeration")
        if element_name == "ReturnType":
            return return_code
        if enums:
            override = self.element_override(element_name, return_code)
            if override is not None:
                return override
            return enums[0].get("value") or "Sample"

        type_name = simple_type.get("name")
        override = self.element_override(element_name, return_code)
        if override is not None:
            return override
        type_override = self.type_override(type_name, element_name)
        if type_override is not None:
            return type_override

        return self.sample_for_builtin(local_name(restriction.get("base")), element_name, return_code)

    def element_override(self, element_name: str, return_code: str) -> str | None:
        overrides = {
            "BankLicenceNo": "DMB/INT/2024/001",
            "BankName": "Sample Commercial Bank Nigeria Plc",
            "ReturnType": return_code,
            "ReportingPeriodStart": DEFAULT_START_DATE,
            "ReportingPeriodEnd": DEFAULT_DATE,
            "SubmissionDateTime": DEFAULT_DATETIME,
            "ContactPerson": "Adebayo Ogundimu",
            "ContactEmail": "regulatory.reporting@samplebank.com",
            "ContactPhone": "+234-1-200-0000",
            "Jurisdiction": "NGA",
            "Description": "Sample Description",
            "ScenarioName": "Baseline Stress Scenario",
            "ScenarioDescription": "Sample macro-financial stress scenario",
            "CurrencyPair": "USD/NGN",
            "MetricName": "Sample Metric",
            "FacilityName": "Sample Facility",
            "CounterpartyName": "Sample Counterparty",
            "DirectorName": "Sample Director",
            "CommitteeName": "Board Risk Committee",
            "ShareholderName": "Sample HoldCo Plc",
            "Name": "Sample Officer",
            "Designation": "Regulatory Officer",
            "CertificationStatement": (
                "I/We certify that the information provided in this return is true, accurate, "
                "and complete to the best of my/our knowledge and belief, and in compliance "
                "with applicable prudential regulations."
            ),
        }
        return overrides.get(element_name)

    def type_override(self, type_name: str | None, element_name: str) -> str | None:
        overrides = {
            "LicenceNoType": "DMB/INT/2024/001",
            "NameType": "Sample Name",
            "EmailType": "regulatory.reporting@samplebank.com",
            "PhoneType": "+234-1-200-0000",
            "CurrencyCodeType": "NGN",
            "CountryCodeType": "NGA",
            "AmountType": "1000.00",
            "RatioType": "10.00",
        }
        if type_name in overrides:
            return overrides[type_name]
        if element_name.endswith("DateTime"):
            return DEFAULT_DATETIME
        if element_name.endswith("Date"):
            return DEFAULT_DATE
        return None

    def sample_for_builtin(self, builtin_type: str | None, element_name: str, return_code: str) -> str:
        override = self.element_override(element_name, return_code)
        if override is not None:
            return override

        builtin = (builtin_type or "string").replace("xs:", "")
        if builtin in {"boolean"}:
            return "false"
        if builtin in {"date"}:
            return DEFAULT_DATE
        if builtin in {"dateTime"}:
            return DEFAULT_DATETIME
        if builtin in {"decimal", "float", "double"}:
            return "1000.00"
        if builtin in {"integer", "int", "nonNegativeInteger", "positiveInteger"}:
            return "1"
        return self.generic_text_value(element_name)

    def generic_text_value(self, element_name: str) -> str:
        lowered = element_name.lower()
        if "email" in lowered:
            return "regulatory.reporting@samplebank.com"
        if "phone" in lowered:
            return "+234-1-200-0000"
        if "currency" in lowered:
            return "NGN"
        if "country" in lowered or "jurisdiction" in lowered:
            return "NGA"
        if "licence" in lowered:
            return "DMB/INT/2024/001"
        if "name" in lowered:
            return "Sample Name"
        if "description" in lowered or "narrative" in lowered or "remarks" in lowered:
            return "Sample Description"
        return "Sample"

    def validate(self, xml_path: Path) -> None:
        xml_doc = etree.parse(str(xml_path))
        self.schema.assertValid(xml_doc)


def main() -> None:
    base = Path(__file__).resolve().parent
    generator = DmbSampleGenerator(
        xsd_path=base / "DMB_Return_Schema_v1.0.xsd",
        output_dir=base,
    )
    generated = generator.generate_all()
    for path in generated:
        print(path.name)


if __name__ == "__main__":
    main()
