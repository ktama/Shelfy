//! ホットキー文字列の解析（doc/SPECIFICATION.md 第 6.2 節）。
//!
//! `修飾キー+修飾キー+キー` の形を読む。
//! 押下中かどうかの判定に修飾キーの集合が要るため、ここで取り出しておく。

use serde::{Deserialize, Serialize};

/// ホットキーの構成
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct HotkeySpec {
    pub ctrl: bool,
    pub shift: bool,
    pub alt: bool,
    pub win: bool,
    /// 修飾キー以外の部分。空にはならない。
    pub key: String,
}

impl HotkeySpec {
    /// 修飾キーが 1 つも無い組み合わせは、誤って全体を占有しやすいので避ける
    pub fn has_modifier(&self) -> bool {
        self.ctrl || self.shift || self.alt || self.win
    }

    /// 表示と保存に使う正規形
    pub fn to_string_normalized(&self) -> String {
        let mut parts = Vec::new();
        if self.ctrl {
            parts.push("Ctrl");
        }
        if self.shift {
            parts.push("Shift");
        }
        if self.alt {
            parts.push("Alt");
        }
        if self.win {
            parts.push("Win");
        }
        parts.push(&self.key);
        parts.join("+")
    }
}

/// 解析できなければ `None` を返す。呼び出し側は既定値を使う。
pub fn parse(text: &str) -> Option<HotkeySpec> {
    let mut spec = HotkeySpec {
        ctrl: false,
        shift: false,
        alt: false,
        win: false,
        key: String::new(),
    };

    for part in text.split('+') {
        let part = part.trim();
        if part.is_empty() {
            continue;
        }
        match part.to_ascii_lowercase().as_str() {
            "ctrl" | "control" => spec.ctrl = true,
            "shift" => spec.shift = true,
            "alt" => spec.alt = true,
            "win" | "windows" | "super" | "meta" => spec.win = true,
            _ => {
                // 修飾キー以外が 2 つ以上あれば解析できない
                if !spec.key.is_empty() {
                    return None;
                }
                spec.key = part.to_string();
            }
        }
    }

    if spec.key.is_empty() {
        return None;
    }
    Some(spec)
}

/// 解析できない場合に使う既定のホットキー
pub fn default_spec() -> HotkeySpec {
    parse(crate::ports::settings_keys::DEFAULT_GLOBAL_HOTKEY).expect("既定値は解析できる")
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn reads_the_default_combination() {
        let s = parse("Ctrl+Shift+Space").unwrap();
        assert!(s.ctrl && s.shift);
        assert!(!s.alt && !s.win);
        assert_eq!(s.key, "Space");
    }

    #[test]
    fn ignores_the_case_of_modifier_names() {
        let s = parse("CTRL+shift+Alt+WIN+F2").unwrap();
        assert!(s.ctrl && s.shift && s.alt && s.win);
        assert_eq!(s.key, "F2");
    }

    #[test]
    fn accepts_the_common_aliases_for_the_windows_key() {
        for text in ["Win+A", "Windows+A", "Super+A", "Meta+A"] {
            assert!(parse(text).unwrap().win, "{text}");
        }
    }

    #[test]
    fn keeps_the_key_name_as_written() {
        // キー名の解釈はホットキー登録の側に任せる
        assert_eq!(parse("Ctrl+KeyA").unwrap().key, "KeyA");
        assert_eq!(parse("Ctrl+Digit1").unwrap().key, "Digit1");
    }

    #[test]
    fn tolerates_stray_spaces_and_separators() {
        let s = parse(" Ctrl + Shift + Space ").unwrap();
        assert_eq!(s.to_string_normalized(), "Ctrl+Shift+Space");
        assert_eq!(parse("Ctrl++Space").unwrap().key, "Space");
    }

    #[test]
    fn rejects_input_without_a_key() {
        assert_eq!(parse(""), None);
        assert_eq!(parse("   "), None);
        assert_eq!(parse("Ctrl+Shift"), None);
        assert_eq!(parse("+++"), None);
    }

    #[test]
    fn rejects_two_non_modifier_keys() {
        assert_eq!(parse("Ctrl+A+B"), None);
    }

    #[test]
    fn a_bare_key_parses_but_reports_no_modifier() {
        let s = parse("Space").unwrap();
        assert!(!s.has_modifier());
    }

    #[test]
    fn the_normalized_form_orders_modifiers_consistently() {
        assert_eq!(
            parse("Shift+Ctrl+Space").unwrap().to_string_normalized(),
            "Ctrl+Shift+Space"
        );
        assert_eq!(
            parse("Win+Alt+Delete").unwrap().to_string_normalized(),
            "Alt+Win+Delete"
        );
    }

    #[test]
    fn the_default_value_from_the_settings_is_parsable() {
        let s = default_spec();
        assert_eq!(s.to_string_normalized(), "Ctrl+Shift+Space");
    }
}
