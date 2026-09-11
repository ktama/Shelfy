//! 設定の読み出し（doc/SPECIFICATION.md 第 8 節）。
//!
//! 設定は文字列のキーと値で持つ。
//! 解釈は読み手側の責任であり、解析できない値は既定値で置き換えてエラーにしない。
//! 「解析できなければ既定値」という規則をここ 1 か所に置き、
//! 画面へ渡す側と起動時に読む側で振る舞いが食い違わないようにする。

use crate::ports::{settings_keys, SettingsRepository};

/// 解釈済みの設定一式
#[derive(Debug, Clone, PartialEq)]
pub struct Settings {
    pub global_hotkey: String,
    pub window_width: f64,
    pub window_height: f64,
    pub start_minimized: bool,
    pub recent_items_count: usize,
}

impl Default for Settings {
    fn default() -> Self {
        Self {
            global_hotkey: settings_keys::DEFAULT_GLOBAL_HOTKEY.to_string(),
            window_width: settings_keys::DEFAULT_WINDOW_WIDTH,
            window_height: settings_keys::DEFAULT_WINDOW_HEIGHT,
            start_minimized: settings_keys::DEFAULT_START_MINIMIZED,
            recent_items_count: settings_keys::DEFAULT_RECENT_ITEMS_COUNT,
        }
    }
}

/// 保存されている値を読み、解析できないものは既定値に落とす。
pub fn read_settings(repo: &impl SettingsRepository) -> Settings {
    let value = |key: &str| repo.get(key);

    Settings {
        global_hotkey: value(settings_keys::GLOBAL_HOTKEY)
            .filter(|v| !v.trim().is_empty())
            .unwrap_or_else(|| settings_keys::DEFAULT_GLOBAL_HOTKEY.to_string()),
        window_width: value(settings_keys::WINDOW_WIDTH)
            .and_then(|v| parse_size(&v))
            .unwrap_or(settings_keys::DEFAULT_WINDOW_WIDTH),
        window_height: value(settings_keys::WINDOW_HEIGHT)
            .and_then(|v| parse_size(&v))
            .unwrap_or(settings_keys::DEFAULT_WINDOW_HEIGHT),
        // 真偽値は "true" のときだけ有効とする
        start_minimized: value(settings_keys::START_MINIMIZED)
            .map(|v| v == "true")
            .unwrap_or(settings_keys::DEFAULT_START_MINIMIZED),
        recent_items_count: value(settings_keys::RECENT_ITEMS_COUNT)
            .and_then(|v| v.parse::<usize>().ok())
            .unwrap_or(settings_keys::DEFAULT_RECENT_ITEMS_COUNT),
    }
}

/// ウィンドウの大きさとして意味のある値だけを受ける。
/// 0 や負の値、無限大を通すと、復元時に開けないウィンドウができる。
fn parse_size(value: &str) -> Option<f64> {
    value
        .parse::<f64>()
        .ok()
        .filter(|v| v.is_finite() && *v > 0.0)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ports::SettingsRepository;
    use crate::testing::InMemorySettingsRepository;

    fn store_with(pairs: &[(&str, &str)]) -> InMemorySettingsRepository {
        let repo = InMemorySettingsRepository::new();
        for (key, value) in pairs {
            repo.set(key, value);
        }
        repo
    }

    #[test]
    fn an_empty_store_yields_the_documented_defaults() {
        let settings = read_settings(&store_with(&[]));

        assert_eq!(settings.global_hotkey, "Ctrl+Shift+Space");
        assert_eq!(settings.window_width, 800.0);
        assert_eq!(settings.window_height, 500.0);
        assert!(!settings.start_minimized);
        assert_eq!(settings.recent_items_count, 20);
        assert_eq!(settings, Settings::default());
    }

    #[test]
    fn saved_values_are_read_back() {
        let settings = read_settings(&store_with(&[
            (settings_keys::GLOBAL_HOTKEY, "Alt+Space"),
            (settings_keys::WINDOW_WIDTH, "1024"),
            (settings_keys::WINDOW_HEIGHT, "640.5"),
            (settings_keys::START_MINIMIZED, "true"),
            (settings_keys::RECENT_ITEMS_COUNT, "5"),
        ]));

        assert_eq!(settings.global_hotkey, "Alt+Space");
        assert_eq!(settings.window_width, 1024.0);
        assert_eq!(settings.window_height, 640.5);
        assert!(settings.start_minimized);
        assert_eq!(settings.recent_items_count, 5);
    }

    /// 第 8 節：解析できない値は既定値で置き換え、エラーにしない。
    #[test]
    fn values_that_cannot_be_parsed_fall_back_to_the_defaults() {
        let settings = read_settings(&store_with(&[
            (settings_keys::WINDOW_WIDTH, "ひろい"),
            (settings_keys::WINDOW_HEIGHT, ""),
            (settings_keys::RECENT_ITEMS_COUNT, "20 件"),
        ]));

        assert_eq!(settings.window_width, 800.0);
        assert_eq!(settings.window_height, 500.0);
        assert_eq!(settings.recent_items_count, 20);
    }

    /// 復元したときに開けないウィンドウを作らない。
    #[test]
    fn a_window_size_that_would_be_unusable_falls_back() {
        for bad in ["0", "-1", "inf", "NaN"] {
            let settings = read_settings(&store_with(&[
                (settings_keys::WINDOW_WIDTH, bad),
                (settings_keys::WINDOW_HEIGHT, bad),
            ]));
            assert_eq!(settings.window_width, 800.0, "幅 {bad}");
            assert_eq!(settings.window_height, 500.0, "高さ {bad}");
        }
    }

    /// 空のホットキーを通すと、登録に失敗して呼び出せなくなる。
    #[test]
    fn a_blank_hotkey_falls_back_to_the_default() {
        let settings = read_settings(&store_with(&[(settings_keys::GLOBAL_HOTKEY, "   ")]));

        assert_eq!(settings.global_hotkey, "Ctrl+Shift+Space");
    }

    /// 第 8 節：`"true"` のときだけ有効にする。
    #[test]
    fn start_minimized_is_only_true_for_the_exact_word() {
        for value in ["false", "True", "1", "yes", ""] {
            let settings = read_settings(&store_with(&[(settings_keys::START_MINIMIZED, value)]));
            assert!(!settings.start_minimized, "{value:?} は有効にしない");
        }

        let settings = read_settings(&store_with(&[(settings_keys::START_MINIMIZED, "true")]));
        assert!(settings.start_minimized);
    }

    #[test]
    fn the_recent_count_accepts_zero() {
        let settings = read_settings(&store_with(&[(settings_keys::RECENT_ITEMS_COUNT, "0")]));

        assert_eq!(settings.recent_items_count, 0, "0 は解析できる値である");
    }
}
