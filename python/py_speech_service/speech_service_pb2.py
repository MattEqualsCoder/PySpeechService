# -*- coding: utf-8 -*-
# Generated by the protocol buffer compiler.  DO NOT EDIT!
# NO CHECKED-IN PROTOBUF GENCODE
# source: speech_service.proto
# Protobuf Python Version: 5.29.0
"""Generated protocol buffer code."""
from google.protobuf import descriptor as _descriptor
from google.protobuf import descriptor_pool as _descriptor_pool
from google.protobuf import runtime_version as _runtime_version
from google.protobuf import symbol_database as _symbol_database
from google.protobuf.internal import builder as _builder
_runtime_version.ValidateProtobufRuntimeVersion(
    _runtime_version.Domain.PUBLIC,
    5,
    29,
    0,
    '',
    'speech_service.proto'
)
# @@protoc_insertion_point(imports)

_sym_db = _symbol_database.Default()




DESCRIPTOR = _descriptor_pool.Default().AddSerializedFile(b'\n\x14speech_service.proto\"\xf9\x02\n\x14SpeechServiceRequest\x12\x42\n\x18start_speech_recognition\x18\x01 \x01(\x0b\x32\x1e.StartSpeechRecognitionRequestH\x00\x12\x38\n\x13set_speech_settings\x18\x02 \x01(\x0b\x32\x19.SetSpeechSettingsRequestH\x00\x12\x1e\n\x05speak\x18\x03 \x01(\x0b\x32\r.SpeakRequestH\x00\x12-\n\rstop_speaking\x18\x04 \x01(\x0b\x32\x14.StopSpeakingRequestH\x00\x12$\n\x08shutdown\x18\x05 \x01(\x0b\x32\x10.ShutdownRequestH\x00\x12\x1c\n\x04ping\x18\x06 \x01(\x0b\x32\x0c.PingRequestH\x00\x12@\n\x17stop_speech_recognition\x18\x07 \x01(\x0b\x32\x1d.StopSpeechRecognitionRequestH\x00\x42\x0e\n\x0cmessage_type\"\xd5\x02\n\x15SpeechServiceResponse\x12,\n\x0cspeak_update\x18\x01 \x01(\x0b\x32\x14.SpeakUpdateResponseH\x00\x12\x37\n\x11speech_recognized\x18\x02 \x01(\x0b\x32\x1a.SpeechRecognitionResponseH\x00\x12$\n\x05\x65rror\x18\x03 \x01(\x0b\x32\x13.SpeechServiceErrorH\x00\x12\x1d\n\x04ping\x18\x04 \x01(\x0b\x32\r.PingResponseH\x00\x12\x45\n\x1aspeech_recognition_started\x18\x05 \x01(\x0b\x32\x1f.StartSpeechRecognitionResponseH\x00\x12\x39\n\x13speech_settings_set\x18\x06 \x01(\x0b\x32\x1a.SetSpeechSettingsResponseH\x00\x42\x0e\n\x0cmessage_type\">\n\x12SpeechServiceError\x12\x15\n\rerror_message\x18\x01 \x01(\t\x12\x11\n\texception\x18\x02 \x01(\t\"f\n\x1dStartSpeechRecognitionRequest\x12\x12\n\nvosk_model\x18\x01 \x01(\t\x12\x14\n\x0cgrammar_file\x18\x02 \x01(\t\x12\x1b\n\x13required_confidence\x18\x03 \x01(\x01\"\x1e\n\x1cStopSpeechRecognitionRequest\"\x8d\x01\n\x18SetSpeechSettingsRequest\x12(\n\x0fspeech_settings\x18\x01 \x01(\x0b\x32\x0f.SpeechSettings\x12\x30\n\x12\x61lt_voice_settings\x18\x02 \x01(\x0b\x32\x0f.SpeechSettingsH\x00\x88\x01\x01\x42\x15\n\x13_alt_voice_settings\"\xc0\x01\n\x0eSpeechSettings\x12\x12\n\nmodel_name\x18\x01 \x01(\t\x12\x11\n\tonnx_path\x18\x02 \x01(\t\x12\x13\n\x0b\x63onfig_path\x18\x03 \x01(\t\x12\x16\n\x0e\x61lt_model_name\x18\x04 \x01(\t\x12\x15\n\ralt_onnx_path\x18\x05 \x01(\t\x12\x17\n\x0f\x61lt_config_path\x18\x06 \x01(\t\x12\r\n\x05speed\x18\x07 \x01(\x01\x12\x0c\n\x04gain\x18\x08 \x01(\x01\x12\r\n\x05pitch\x18\t \x01(\x01\"b\n\x0cSpeakRequest\x12\x0f\n\x07message\x18\x01 \x01(\t\x12-\n\x0fspeech_settings\x18\x02 \x01(\x0b\x32\x0f.SpeechSettingsH\x00\x88\x01\x01\x42\x12\n\x10_speech_settings\"\x15\n\x13StopSpeakingRequest\"\xbe\x01\n\x13SpeakUpdateResponse\x12\x0f\n\x07message\x18\x01 \x01(\t\x12\r\n\x05\x63hunk\x18\x02 \x01(\t\x12\x1b\n\x13is_start_of_message\x18\x03 \x01(\x08\x12\x19\n\x11is_start_of_chunk\x18\x04 \x01(\x08\x12\x19\n\x11is_end_of_message\x18\x05 \x01(\x08\x12\x17\n\x0fis_end_of_chunk\x18\x06 \x01(\x08\x12\x1b\n\x13has_another_request\x18\x07 \x01(\x08\"\xe5\x01\n\x19SpeechRecognitionResponse\x12\x12\n\nheard_text\x18\x01 \x01(\t\x12\x17\n\x0frecognized_text\x18\x02 \x01(\t\x12\x17\n\x0frecognized_rule\x18\x03 \x01(\t\x12\x12\n\nconfidence\x18\x04 \x01(\x01\x12<\n\tsemantics\x18\x05 \x03(\x0b\x32).SpeechRecognitionResponse.SemanticsEntry\x1a\x30\n\x0eSemanticsEntry\x12\x0b\n\x03key\x18\x01 \x01(\t\x12\r\n\x05value\x18\x02 \x01(\t:\x02\x38\x01\"\x11\n\x0fShutdownRequest\"\x1b\n\x0bPingRequest\x12\x0c\n\x04time\x18\x01 \x01(\t\"\x1c\n\x0cPingResponse\x12\x0c\n\x04time\x18\x01 \x01(\t\"4\n\x1eStartSpeechRecognitionResponse\x12\x12\n\nsuccessful\x18\x01 \x01(\x08\"/\n\x19SetSpeechSettingsResponse\x12\x12\n\nsuccessful\x18\x01 \x01(\x08\x32X\n\rSpeechService\x12G\n\x12StartSpeechService\x12\x15.SpeechServiceRequest\x1a\x16.SpeechServiceResponse(\x01\x30\x01\x62\x06proto3')

_globals = globals()
_builder.BuildMessageAndEnumDescriptors(DESCRIPTOR, _globals)
_builder.BuildTopDescriptorsAndMessages(DESCRIPTOR, 'speech_service_pb2', _globals)
if not _descriptor._USE_C_DESCRIPTORS:
  DESCRIPTOR._loaded_options = None
  _globals['_SPEECHRECOGNITIONRESPONSE_SEMANTICSENTRY']._loaded_options = None
  _globals['_SPEECHRECOGNITIONRESPONSE_SEMANTICSENTRY']._serialized_options = b'8\001'
  _globals['_SPEECHSERVICEREQUEST']._serialized_start=25
  _globals['_SPEECHSERVICEREQUEST']._serialized_end=402
  _globals['_SPEECHSERVICERESPONSE']._serialized_start=405
  _globals['_SPEECHSERVICERESPONSE']._serialized_end=746
  _globals['_SPEECHSERVICEERROR']._serialized_start=748
  _globals['_SPEECHSERVICEERROR']._serialized_end=810
  _globals['_STARTSPEECHRECOGNITIONREQUEST']._serialized_start=812
  _globals['_STARTSPEECHRECOGNITIONREQUEST']._serialized_end=914
  _globals['_STOPSPEECHRECOGNITIONREQUEST']._serialized_start=916
  _globals['_STOPSPEECHRECOGNITIONREQUEST']._serialized_end=946
  _globals['_SETSPEECHSETTINGSREQUEST']._serialized_start=949
  _globals['_SETSPEECHSETTINGSREQUEST']._serialized_end=1090
  _globals['_SPEECHSETTINGS']._serialized_start=1093
  _globals['_SPEECHSETTINGS']._serialized_end=1285
  _globals['_SPEAKREQUEST']._serialized_start=1287
  _globals['_SPEAKREQUEST']._serialized_end=1385
  _globals['_STOPSPEAKINGREQUEST']._serialized_start=1387
  _globals['_STOPSPEAKINGREQUEST']._serialized_end=1408
  _globals['_SPEAKUPDATERESPONSE']._serialized_start=1411
  _globals['_SPEAKUPDATERESPONSE']._serialized_end=1601
  _globals['_SPEECHRECOGNITIONRESPONSE']._serialized_start=1604
  _globals['_SPEECHRECOGNITIONRESPONSE']._serialized_end=1833
  _globals['_SPEECHRECOGNITIONRESPONSE_SEMANTICSENTRY']._serialized_start=1785
  _globals['_SPEECHRECOGNITIONRESPONSE_SEMANTICSENTRY']._serialized_end=1833
  _globals['_SHUTDOWNREQUEST']._serialized_start=1835
  _globals['_SHUTDOWNREQUEST']._serialized_end=1852
  _globals['_PINGREQUEST']._serialized_start=1854
  _globals['_PINGREQUEST']._serialized_end=1881
  _globals['_PINGRESPONSE']._serialized_start=1883
  _globals['_PINGRESPONSE']._serialized_end=1911
  _globals['_STARTSPEECHRECOGNITIONRESPONSE']._serialized_start=1913
  _globals['_STARTSPEECHRECOGNITIONRESPONSE']._serialized_end=1965
  _globals['_SETSPEECHSETTINGSRESPONSE']._serialized_start=1967
  _globals['_SETSPEECHSETTINGSRESPONSE']._serialized_end=2014
  _globals['_SPEECHSERVICE']._serialized_start=2016
  _globals['_SPEECHSERVICE']._serialized_end=2104
# @@protoc_insertion_point(module_scope)
