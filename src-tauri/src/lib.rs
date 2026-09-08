//! Shelfy のバックエンド。
//!
//! 層の分け方は doc/rebuild/03-ARCHITECTURE.md に、
//! 振る舞いの規則は doc/rebuild/02-SPECIFICATION.md に対応する。
//!
//! `domain` と `usecases` は UI も OS も永続化技術も知らない。
//! 外界とのやり取りは `ports` の trait を通す。

pub mod domain;
pub mod ports;
pub mod usecases;

#[cfg(any(test, feature = "testing"))]
pub mod testing;
