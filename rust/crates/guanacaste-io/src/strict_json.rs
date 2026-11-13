//! JSON parser that rejects duplicate keys at every nesting level.
//! serde_json::Value alone keeps the last occurrence and is unsafe for
//! document identity checks where two readers might interpret keys differently.
#![forbid(unsafe_code)]

use serde::de::{Deserialize, Deserializer, MapAccess, SeqAccess, Visitor};
use serde_json::{Map, Number, Value};
use std::collections::HashSet;
use std::fmt;

const DUPLICATE_KEY: &str = "duplicate JSON object key";

#[derive(Debug)]
struct StrictValue(Value);

impl<'de> Deserialize<'de> for StrictValue {
    fn deserialize<D: Deserializer<'de>>(deserializer: D) -> Result<Self, D::Error> {
        deserializer.deserialize_any(StrictVisitor)
    }
}

struct StrictVisitor;

impl<'de> Visitor<'de> for StrictVisitor {
    type Value = StrictValue;

    fn expecting(&self, f: &mut fmt::Formatter) -> fmt::Result {
        f.write_str("a JSON value without duplicate object keys")
    }

    fn visit_unit<E: serde::de::Error>(self) -> Result<Self::Value, E> {
        Ok(StrictValue(Value::Null))
    }

    fn visit_none<E: serde::de::Error>(self) -> Result<Self::Value, E> {
        Ok(StrictValue(Value::Null))
    }

    fn visit_bool<E: serde::de::Error>(self, v: bool) -> Result<Self::Value, E> {
        Ok(StrictValue(Value::Bool(v)))
    }

    fn visit_i64<E: serde::de::Error>(self, v: i64) -> Result<Self::Value, E> {
        Ok(StrictValue(Value::Number(v.into())))
    }

    fn visit_u64<E: serde::de::Error>(self, v: u64) -> Result<Self::Value, E> {
        Ok(StrictValue(Value::Number(v.into())))
    }

    fn visit_f64<E: serde::de::Error>(self, v: f64) -> Result<Self::Value, E> {
        let number = Number::from_f64(v).ok_or_else(|| E::custom("invalid JSON number"))?;
        Ok(StrictValue(Value::Number(number)))
    }

    fn visit_str<E: serde::de::Error>(self, v: &str) -> Result<Self::Value, E> {
        Ok(StrictValue(Value::String(v.to_owned())))
    }

    fn visit_string<E: serde::de::Error>(self, v: String) -> Result<Self::Value, E> {
        Ok(StrictValue(Value::String(v)))
    }

    fn visit_seq<A: SeqAccess<'de>>(self, mut seq: A) -> Result<Self::Value, A::Error> {
        let mut items = Vec::new();
        while let Some(StrictValue(item)) = seq.next_element::<StrictValue>()? {
            items.push(item);
        }
        Ok(StrictValue(Value::Array(items)))
    }

    fn visit_map<A: MapAccess<'de>>(self, mut map: A) -> Result<Self::Value, A::Error> {
        let mut seen = HashSet::new();
        let mut fields = Map::new();
        while let Some(key) = map.next_key::<String>()? {
            if !seen.insert(key.clone()) {
                return Err(serde::de::Error::custom(DUPLICATE_KEY));
            }
            let StrictValue(value) = map.next_value::<StrictValue>()?;
            fields.insert(key, value);
        }
        Ok(StrictValue(Value::Object(fields)))
    }
}

/// Returns a JSON tree only when every object has unique keys.
pub(crate) fn parse(bytes: &[u8]) -> Result<Value, crate::FileError> {
    let mut deserializer = serde_json::Deserializer::from_slice(bytes);
    let StrictValue(value) = StrictValue::deserialize(&mut deserializer).map_err(classify)?;
    deserializer.end().map_err(classify)?;
    Ok(value)
}

fn classify(error: serde_json::Error) -> crate::FileError {
    if error.to_string().contains(DUPLICATE_KEY) {
        crate::FileError::DuplicateProjectField
    } else {
        crate::FileError::InvalidJson
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn detects_repeated_fields_in_nested_array_objects() {
        assert_eq!(
            parse(br#"{"Levels":[{"Id":1,"Id":2}]}"#),
            Err(crate::FileError::DuplicateProjectField)
        );
    }

    #[test]
    fn detects_same_field_after_json_escape_decoding() {
        assert_eq!(
            parse(br#"{"Id":"a","\u0049d":"b"}"#),
            Err(crate::FileError::DuplicateProjectField)
        );
    }

    #[test]
    fn allows_same_key_in_different_objects() {
        assert!(parse(br#"[{"Name":"first"},{"Name":"second"}]"#).is_ok());
    }

    #[test]
    fn rejects_trailing_json_after_object() {
        assert_eq!(
            parse(br#"{"a":1} {"b":2}"#),
            Err(crate::FileError::InvalidJson)
        );
    }
}
