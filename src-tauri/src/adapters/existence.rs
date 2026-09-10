//! 存在確認の結果を短いあいだ覚えておく包み（doc/SPECIFICATION.md 第 7.5 節）。
//!
//! 一覧に同じ参照先が何度も出ることと、切断されたネットワークドライブで
//! 1 件あたりの待ち時間が跳ね上がることの、両方に効く。

use std::collections::HashMap;
use std::sync::Mutex;
use std::time::{Duration, Instant};

use crate::ports::ExistenceChecker;

/// 結果を再利用する既定の長さ
pub const DEFAULT_TTL: Duration = Duration::from_secs(30);

pub struct CachedExistenceChecker<T: ExistenceChecker> {
    inner: T,
    ttl: Duration,
    entries: Mutex<HashMap<String, (bool, Instant)>>,
}

impl<T: ExistenceChecker> CachedExistenceChecker<T> {
    pub fn new(inner: T, ttl: Duration) -> Self {
        Self {
            inner,
            ttl,
            entries: Mutex::new(HashMap::new()),
        }
    }

    /// 覚えている結果をすべて捨てる。参照先が変わったときに呼ぶ。
    pub fn clear(&self) {
        self.entries.lock().unwrap().clear();
    }

    /// 覚えている件数。試験と診断に使う。
    pub fn len(&self) -> usize {
        self.entries.lock().unwrap().len()
    }

    pub fn is_empty(&self) -> bool {
        self.len() == 0
    }
}

impl<T: ExistenceChecker> ExistenceChecker for CachedExistenceChecker<T> {
    fn exists(&self, target: &str) -> bool {
        // 覚えていて、まだ新しければそれを返す
        if let Some((found, at)) = self.entries.lock().unwrap().get(target) {
            if at.elapsed() < self.ttl {
                return *found;
            }
        }

        // 実際の確認は、覚え書きの鍵を持たないまま行う。
        // ファイルへの問い合わせで待つあいだ、他の確認を止めないため。
        let found = self.inner.exists(target);

        self.entries
            .lock()
            .unwrap()
            .insert(target.to_string(), (found, Instant::now()));
        found
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::atomic::{AtomicUsize, Ordering};

    /// 何回問い合わせられたかを数える
    struct Counting {
        answer: bool,
        calls: AtomicUsize,
    }

    impl Counting {
        fn new(answer: bool) -> Self {
            Self {
                answer,
                calls: AtomicUsize::new(0),
            }
        }

        fn calls(&self) -> usize {
            self.calls.load(Ordering::SeqCst)
        }
    }

    impl ExistenceChecker for Counting {
        fn exists(&self, _target: &str) -> bool {
            self.calls.fetch_add(1, Ordering::SeqCst);
            self.answer
        }
    }

    #[test]
    fn the_same_target_is_only_asked_once() {
        let cache = CachedExistenceChecker::new(Counting::new(true), Duration::from_secs(60));

        assert!(cache.exists("C:\\a.txt"));
        assert!(cache.exists("C:\\a.txt"));
        assert!(cache.exists("C:\\a.txt"));

        assert_eq!(cache.inner.calls(), 1);
    }

    #[test]
    fn different_targets_are_asked_separately() {
        let cache = CachedExistenceChecker::new(Counting::new(false), Duration::from_secs(60));

        assert!(!cache.exists("C:\\a.txt"));
        assert!(!cache.exists("C:\\b.txt"));

        assert_eq!(cache.inner.calls(), 2);
        assert_eq!(cache.len(), 2);
    }

    #[test]
    fn an_expired_answer_is_asked_again() {
        let cache = CachedExistenceChecker::new(Counting::new(true), Duration::ZERO);

        cache.exists("C:\\a.txt");
        cache.exists("C:\\a.txt");

        // 覚えている長さが 0 なら、毎回聞き直す
        assert_eq!(cache.inner.calls(), 2);
    }

    #[test]
    fn clearing_forgets_everything() {
        let cache = CachedExistenceChecker::new(Counting::new(true), Duration::from_secs(60));

        cache.exists("C:\\a.txt");
        assert_eq!(cache.len(), 1);

        cache.clear();
        assert!(cache.is_empty());

        cache.exists("C:\\a.txt");
        assert_eq!(cache.inner.calls(), 2);
    }

    #[test]
    fn the_answer_itself_is_carried_through() {
        let yes = CachedExistenceChecker::new(Counting::new(true), DEFAULT_TTL);
        let no = CachedExistenceChecker::new(Counting::new(false), DEFAULT_TTL);

        assert!(yes.exists("C:\\a.txt"));
        assert!(!no.exists("C:\\a.txt"));
    }
}
