import json
import logging
import random
import re
import typing

import num2words
from rapidfuzz import process

from py_speech_service.grammar_element import GrammarRuleElement, GrammarElementType, GrammarElementLookupItem, \
    GrammarElementMatch


class GrammarParser:

    phrase_map: dict[str, list[GrammarElementLookupItem]] = {}
    leading_phrases: list[str] = []
    pattern = re.compile('[^\\w ]+')
    max_phrase_word_count: int = 0
    all_words: list[str] = []
    phrase_replacement_map: dict[str, list[str]]
    phrase_replacement_regex: str
    replacement_map: dict[str, str]
    replacement_regex: str
    has_replacements: bool = False
    prefix: list[str]

    def set_grammar_file(self, file_path: str):
        with open(file_path, 'r') as fp:
            lines = fp.read()

        self.all_words = []
        self.replacement_map = {}
        self.replacement_regex = ""
        self.phrase_replacement_map = {}
        self.phrase_replacement_regex = ""

        json_data = json.loads(lines)

        logging.info("Loaded grammar json data file " + file_path)

        if "Replacements" in json_data and json_data["Replacements"] and len(json_data["Replacements"]) > 0:
            for find_text, replace_with in json_data["Replacements"].items():
                find_text_str = str(find_text).lower()
                replace_with_str = str(replace_with).lower()
                self.replacement_map[find_text_str] = replace_with_str
                self.all_words.append(find_text_str)

                if not self.phrase_replacement_map.__contains__(replace_with_str):
                    self.phrase_replacement_map[replace_with_str] = []
                self.phrase_replacement_map[replace_with_str].append(find_text_str)

            logging.info("Loaded " + str(len(json_data["Replacements"])) + " replacements")

            sorted_replacement_keys = sorted(self.replacement_map.keys(), key=len, reverse=True)
            self.replacement_regex = r'(' + '|'.join(map(re.escape, sorted_replacement_keys)) + r')'

            sorted_replacement_keys = sorted(self.phrase_replacement_map.keys(), key=len, reverse=True)
            self.phrase_replacement_regex = r'\b(' + '|'.join(map(re.escape, sorted_replacement_keys)) + r')\b'

        for rule in json_data["Rules"]:
            rule_name: str = rule["Key"]
            self.__parse_rule_element(rule_name, rule)

        logging.info("Loaded " + str(len(json_data["Rules"])) + " rules")

        if "Prefix" in json_data:
            self.prefix = json_data["Prefix"].lower().split()
        else:
            self.prefix = []

        self.all_words = list(set(self.all_words))
        updated_words = []
        for word in self.all_words:
            updated_words += self.pattern.sub('', word).lower().split()
        self.all_words = list(set(updated_words))

    def find_match(self, stated_text: str, min_threshold: float = 80, min_prefix_threshold: float = 60):
        search_text = self.pattern.sub('', stated_text).lower().strip()
        search_words = search_text.split()
        search_word_count = len(search_words)
        if search_word_count > 30:
            return None

        if len(self.prefix) > 0:
            prefix_search = " ".join(search_words[:len(self.prefix)])
            prefix_result = self.__find_closest_sentence([" ".join(self.prefix)], prefix_search)
            if prefix_result is None:
                return None
            if prefix_result[1] < min_prefix_threshold:
                return None
            index = 0
            for word in self.prefix:
                search_words[index] = word
                index = index + 1
            search_text = " ".join(search_words)
            logging.info("Matched prefix " + " ".join(self.prefix))

        possibilities: [(str, float)] = []
        for i in range(search_word_count, 1, -1):
            search_phrase = " ".join(search_words[0:i])
            search_result = self.__find_closest_sentence(self.leading_phrases, search_phrase)
            if search_result is not None and search_result[1] > min_threshold:
                possibilities.append(search_result)

        matches: dict[str, GrammarElementMatch] = {}
        searched_phrases: [str] = []
        for possibility in possibilities:
            search_phrase = possibility[0]
            if searched_phrases.__contains__(search_phrase):
                continue
            searched_phrases.append(search_phrase)
            initial_confidence = possibility[1]
            possible_elements = self.phrase_map.get(search_phrase)
            searched_elements = []
            for possible_element in possible_elements:
                if possible_element.is_full_match:
                    if initial_confidence > min_threshold:
                        match = GrammarElementMatch(possible_element.rule_name, stated_text, search_phrase, initial_confidence)
                        matches[match.matched_text] = match
                else:
                    best_item = self.__find_best_element(search_text, search_phrase, possible_element)
                    if best_item is not None and best_item[1] > min_threshold:
                        match = GrammarElementMatch(possible_element.rule_name, stated_text, best_item[0],
                                                    best_item[1], best_item[2])
                        matches[match.matched_text] = match

        if len(matches) == 0:
            return None
        else:
            closest_sentence = self.__find_closest_sentence(matches.keys(), search_text)
            if closest_sentence is None:
                return None
            selected_match = matches[closest_sentence[0]]
            selected_match.confidence = (closest_sentence[1] + selected_match.confidence) / 2

            if selected_match.confidence > 98:
                selected_match.confidence = selected_match.confidence - random.uniform(0.5, 2.5)

            if self.replacement_regex:
                selected_match.matched_text = re.sub(self.replacement_regex, lambda regex_match: self.replacement_map[regex_match.group(0)],
                                     selected_match.matched_text)

            return selected_match

    def __parse_rule_element(self, rule_name: str, rule_element):

        element = GrammarRuleElement()
        element.rule = rule_name
        element.data = []
        element_phrases: list[str] = [""]

        is_exact: bool = True
        words = []

        for sub_element_json in rule_element['Data']:
            element.data.append(sub_element_json)

            element_type: GrammarElementType = GrammarElementType(sub_element_json['Type'])

            if element_type == GrammarElementType.String:
                for i in range(0, len(element_phrases)):
                    text = self.pattern.sub('', sub_element_json['Data'].strip() + " ").lower()
                    words += [ text]
                    if is_exact:
                        element_phrases[i] += text
            elif element_type == GrammarElementType.OneOf:
                one_of_phrases: [str] = sub_element_json['Data']
                words += one_of_phrases
                if is_exact:
                    element_phrases = self.__permutate_elements(element_phrases, one_of_phrases)
            elif element_type == GrammarElementType.Optional:
                optional_phrases: [str] = sub_element_json['Data']
                words += optional_phrases
                if is_exact:
                    optional_phrases.append("")
                    element_phrases = self.__permutate_elements(element_phrases, optional_phrases)
            elif element_type == GrammarElementType.KeyValue:
                items = []
                for key_value_json in sub_element_json['Data']:
                    key = key_value_json['Key']
                    if key.isnumeric():
                        key = num2words.num2words(key).replace("-", " ")
                    key = str(key).lower()

                    if self.phrase_replacement_regex:
                        match = re.search(self.phrase_replacement_regex, key)
                        if match:
                            replacements = self.phrase_replacement_map[match.group()]
                            for replacement in replacements:
                                new_key = str(key).replace(match.group(), replacement)
                                items.append({ "Key": new_key, "Value" : key_value_json['Value']})
                                words.append(new_key)
                        else:
                            items.append(key_value_json)
                            words.append(key)
                    else:
                        items.append(key_value_json)
                        words.append(key)
                sub_element_json['Data'] = items
                is_exact = False
            elif element_type == GrammarElementType.GrammarElementList:
                for grammar_list_item in sub_element_json['Data']:
                    self.__parse_rule_element(rule_name, grammar_list_item)
                return

        self.all_words += words
        for phrase in element_phrases:
            match_details = GrammarElementLookupItem(rule_name, phrase, element, is_exact)
            word_count = len(phrase.split())
            if phrase.strip() == "" or word_count < 2:
                continue
            if word_count > self.max_phrase_word_count:
                self.max_phrase_word_count = word_count
            if self.phrase_map.__contains__(phrase):
                self.phrase_map[phrase].append(match_details)
            else:
                self.phrase_map[phrase] = [ match_details ]
                self.leading_phrases.append(phrase)

    def __permutate_elements(self, initial_items: [str], additional_items: [str]):
        initial_count = len(initial_items)
        to_return = self.__duplicate_elements(initial_items, len(additional_items))
        index = 0
        for i in range(0, initial_count):
            for text in additional_items:
                text = self.pattern.sub('', text.strip() + " ").lower()
                if text == " ":
                    index = index + 1
                    continue
                to_return[index] += text
                index = index + 1
        return to_return

    def __find_best_element(self, search_text: str, current_phrase: str, element: GrammarElementLookupItem):
        hit_key_value = False
        search_phrase = current_phrase
        selected_values: dict[str, str] = {}
        confidence = 0
        search_words = search_text.split()

        for sub_element_json in element.grammar_element.data:
            element_type: GrammarElementType = GrammarElementType(sub_element_json['Type'])
            if not hit_key_value:
                if element_type == GrammarElementType.KeyValue:
                    hit_key_value = True
                else:
                    continue

            if element_type == GrammarElementType.String:
                text = self.pattern.sub('', sub_element_json['Data'].strip() + " ").lower()
                search_phrase += text
            elif element_type == GrammarElementType.OneOf:
                items: [str] = sub_element_json['Data']
                match = self.__find_best_element_sub_phrase(search_phrase, search_words, items)
                if match is None:
                    return None
                else:
                    search_phrase = match[0]
                    confidence = match[1]
            elif element_type == GrammarElementType.Optional:
                items: [str] = sub_element_json['Data']
                match = self.__find_best_element_sub_phrase(search_phrase, search_words, items)
                if match is not None and match[1] > confidence:
                    search_phrase = match[0]
                    confidence = match[1]
            elif element_type == GrammarElementType.KeyValue:
                keys: [str] = []
                key_values: dict[str, str] = {}
                for key_value_json in sub_element_json['Data']:
                    key = key_value_json['Key']
                    if key.isnumeric():
                        key = num2words.num2words(key).replace("-", " ")
                    keys.append(key)
                    key_values[key] = key_value_json['Value']
                match = self.__find_best_element_sub_phrase(search_phrase, search_words, keys)
                if match is None:
                    return None
                else:
                    search_phrase = match[0]
                    confidence = match[1]
                    selected_values[str(sub_element_json['Key'])] = str(key_values[match[2]])

        return search_phrase, confidence, selected_values

    def __find_best_element_sub_phrase(self, search_phrase: str, search_words: [str], items: [str]):
        if not search_phrase.endswith(" "):
            search_phrase = search_phrase + " "
        possible_phrases: [str] = []
        phrase_addition_map: dict[str, str] = {}
        max_word_count = 0
        min_word_count = 10000
        for item in items:
            text = self.pattern.sub('', item.strip() + " ").lower()
            new_phrase = search_phrase + text
            possible_phrases.append(new_phrase)
            phrase_addition_map[new_phrase] = item
            new_phrase_word_count = len(new_phrase.split())
            if new_phrase_word_count < min_word_count:
                min_word_count = new_phrase_word_count
            if new_phrase_word_count > max_word_count:
                max_word_count = new_phrase_word_count

        best_result = None
        previous_search_phrase = ""
        for num_words in range(min_word_count, max_word_count+2):
            new_search_phrase = " ".join(search_words[0:num_words])
            if new_search_phrase == previous_search_phrase:
                break
            previous_search_phrase = new_search_phrase
            result = self.__find_closest_sentence(possible_phrases, new_search_phrase)
            if result is not None:
                if best_result is None:
                    best_result = result
                elif result[1] >= best_result[1] - 2:
                    best_result = result

        if best_result is None:
            return None

        return best_result[0], best_result[1], phrase_addition_map[best_result[0]]

    def __find_closest_sentence(self, sentences: [str], query: str) -> (str, float):
        filtered_sentences = self.__filter_by_length(sentences, query)
        squashed_sentences = []
        squashed_map = {}
        for sentence in filtered_sentences:
            squashed_sentence = sentence.replace(" ", "")
            squashed_sentences.append(squashed_sentence)
            squashed_map[squashed_sentence] = sentence
        response = process.extractOne(query.replace(" ", ""), squashed_sentences)
        if response is None:
            return None
        return squashed_map[response[0]], response[1]

    @staticmethod
    def __filter_by_length(items: [], reference_string: str, tolerance: int = 4):
        reference_length = len(reference_string)
        return [s for s in items if abs(len(s) - reference_length) <= tolerance]

    @staticmethod
    def __duplicate_elements(items: [], times: int):
        if times < 1:
            return []  # Return an empty list if times is less than 1
        return [element for element in items for _ in range(times)]
