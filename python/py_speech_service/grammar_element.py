from enum import Enum

class GrammarElementType(Enum):
    Rule = 0
    String = 1
    OneOf = 2
    Optional = 3
    KeyValue = 4
    GrammarElementList = 5

class GrammarRuleElement:
    def __init(self, rule_name: str):
        self.rule = rule_name
        self.data = []

    rule: str = ""
    data = []

class GrammarElementLookupItem:

    def __init__(self, rule_name: str, phrase: str, element: GrammarRuleElement, full_match: bool):
        self.rule_name = rule_name
        self.grammar_element = element
        self.is_full_match = full_match
        self.phrase = phrase

    rule_name: str
    phrase: str
    grammar_element: GrammarRuleElement
    is_full_match: bool

class GrammarElementMatch:

    def __init__(self, rule_name: str, stated_text: str, matched_text: str, confidence: float, values=None):
        if values is None:
            values = {}
        self.rule = rule_name
        self.stated_text = stated_text
        self.matched_text = matched_text.strip()
        self.confidence = confidence
        self.values = values

    rule: str = ""
    stated_text: str = ""
    matched_text: str = ""
    confidence: float
    values: dict[str, str]