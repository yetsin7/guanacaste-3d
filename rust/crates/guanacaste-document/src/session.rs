//! Bounded, validated document history for the first Rust BIM model slice.
//!
//! The existing .NET DocumentSession remains authoritative until parity is
//! proven. This implementation stores complete snapshots, never partial edits.
#![forbid(unsafe_code)]

use crate::{Document, DocumentError};
use std::collections::VecDeque;

/// Undo/redo history is bounded by a configured number of snapshots.
/// A new successful edit after undo discards the redo chain.
#[derive(Clone, Debug)]
pub struct DocumentSession {
    current: Document,
    undo: VecDeque<Document>,
    redo: VecDeque<Document>,
    capacity: usize,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum SessionError {
    InvalidCapacity,
    InvalidDocument(DocumentError),
}

impl DocumentSession {
    pub fn new(initial: Document, capacity: usize) -> Result<Self, SessionError> {
        if capacity == 0 {
            return Err(SessionError::InvalidCapacity);
        }
        initial.validate().map_err(SessionError::InvalidDocument)?;
        Ok(Self {
            current: initial,
            undo: VecDeque::new(),
            redo: VecDeque::new(),
            capacity,
        })
    }

    pub fn document(&self) -> &Document {
        &self.current
    }

    pub fn can_undo(&self) -> bool {
        !self.undo.is_empty()
    }

    pub fn can_redo(&self) -> bool {
        !self.redo.is_empty()
    }

    pub fn undo_count(&self) -> usize {
        self.undo.len()
    }

    pub fn redo_count(&self) -> usize {
        self.redo.len()
    }

    /// Applies an edit to a cloned candidate; failures preserve current
    /// document and *both* history stacks. No-op edits create no history.
    pub fn edit(
        &mut self,
        update: impl FnOnce(&mut Document) -> Result<(), DocumentError>,
    ) -> Result<bool, DocumentError> {
        let mut candidate = self.current.clone();
        update(&mut candidate)?;
        candidate.validate()?;
        if candidate == self.current {
            return Ok(false);
        }
        let old = std::mem::replace(&mut self.current, candidate);
        push_bounded(&mut self.undo, old, self.capacity);
        self.redo.clear();
        Ok(true)
    }

    pub fn undo(&mut self) -> bool {
        let Some(previous) = self.undo.pop_back() else {
            return false;
        };
        let current = std::mem::replace(&mut self.current, previous);
        push_bounded(&mut self.redo, current, self.capacity);
        true
    }

    pub fn redo(&mut self) -> bool {
        let Some(next) = self.redo.pop_back() else {
            return false;
        };
        let current = std::mem::replace(&mut self.current, next);
        push_bounded(&mut self.undo, current, self.capacity);
        true
    }
}

fn push_bounded(history: &mut VecDeque<Document>, document: Document, capacity: usize) {
    if history.len() == capacity {
        history.pop_front();
    }
    history.push_back(document);
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{HostedOpening, Level, Wall};

    fn sample() -> Document {
        Document {
            levels: vec![Level {
                id: 1,
                name: "Planta baja".into(),
                elevation_m: 0.0,
            }],
            walls: vec![Wall {
                id: 2,
                level_id: 1,
                start: (0.0, 0.0),
                end: (4.0, 0.0),
                thickness_m: 0.2,
                height_m: 3.0,
                base_offset_m: 0.0,
            }],
            openings: vec![HostedOpening {
                id: 3,
                wall_id: 2,
                from_m: 1.0,
                to_m: 2.0,
                sill_m: 1.0,
                head_m: 2.0,
            }],
        }
    }

    #[test]
    fn rejects_zero_capacity_and_invalid_initial_state() {
        assert!(matches!(
            DocumentSession::new(sample(), 0),
            Err(SessionError::InvalidCapacity)
        ));
        let mut invalid = sample();
        invalid.walls[0].level_id = 999;
        assert!(matches!(
            DocumentSession::new(invalid, 2),
            Err(SessionError::InvalidDocument(DocumentError::MissingLevel))
        ));
    }

    #[test]
    fn undo_redo_restore_full_document_and_generated_mesh() {
        let original = sample();
        let old_volume = original.wall_mesh(2).unwrap().signed_volume();
        let mut session = DocumentSession::new(original.clone(), 3).unwrap();
        assert!(session
            .edit(|document| {
                document.walls[0].thickness_m = 0.3;
                Ok(())
            })
            .unwrap());
        assert!(session.can_undo());
        assert!(!session.can_redo());
        let changed_volume = session.document().wall_mesh(2).unwrap().signed_volume();
        assert!(changed_volume > old_volume);
        assert!(session.undo());
        assert_eq!(session.document(), &original);
        assert_eq!(
            session.document().wall_mesh(2).unwrap().signed_volume(),
            old_volume
        );
        assert!(session.redo());
        assert_eq!(
            session.document().wall_mesh(2).unwrap().signed_volume(),
            changed_volume
        );
    }

    #[test]
    fn failure_preserves_redo_and_history() {
        let mut session = DocumentSession::new(sample(), 3).unwrap();
        session
            .edit(|d| {
                d.walls[0].thickness_m = 0.3;
                Ok(())
            })
            .unwrap();
        session.undo();
        let before = session.document().clone();
        let undo_count = session.undo_count();
        let redo_count = session.redo_count();
        assert_eq!(
            session.edit(|d| {
                d.walls[0].thickness_m = -1.0;
                Ok(())
            }),
            Err(DocumentError::InvalidWall)
        );
        assert_eq!(session.document(), &before);
        assert_eq!(session.undo_count(), undo_count);
        assert_eq!(session.redo_count(), redo_count);
        assert!(session.redo());
    }

    #[test]
    fn new_branch_clears_redo_only_on_change() {
        let mut session = DocumentSession::new(sample(), 2).unwrap();
        session
            .edit(|d| {
                d.walls[0].thickness_m = 0.3;
                Ok(())
            })
            .unwrap();
        session.undo();
        assert!(!session.edit(|_| Ok(())).unwrap());
        assert!(session.can_redo());
        session
            .edit(|d| {
                d.walls[0].height_m = 4.0;
                Ok(())
            })
            .unwrap();
        assert!(!session.can_redo());
        assert!(!session.redo());
        assert_eq!(session.document().walls[0].height_m, 4.0);
    }

    #[test]
    fn history_capacity_drops_oldest_snapshot() {
        let mut session = DocumentSession::new(sample(), 2).unwrap();
        for thickness in [0.3, 0.4, 0.5] {
            session
                .edit(|d| {
                    d.walls[0].thickness_m = thickness;
                    Ok(())
                })
                .unwrap();
        }
        assert_eq!(session.undo_count(), 2);
        assert!(session.undo());
        assert_eq!(session.document().walls[0].thickness_m, 0.4);
        assert!(session.undo());
        assert_eq!(session.document().walls[0].thickness_m, 0.3);
        assert!(!session.undo());
    }

    #[test]
    fn edit_callback_error_is_atomic() {
        let mut session = DocumentSession::new(sample(), 2).unwrap();
        let before = session.document().clone();
        assert_eq!(
            session.edit(|d| {
                d.walls[0].thickness_m = 0.8;
                Err(DocumentError::InvalidOpening)
            }),
            Err(DocumentError::InvalidOpening)
        );
        assert_eq!(session.document(), &before);
        assert!(!session.can_undo());
    }
}
