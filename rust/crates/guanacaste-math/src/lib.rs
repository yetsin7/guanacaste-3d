//! Metric, right-handed, Z-up geometry primitives for Guanacaste 3D.
//! UI coordinate conversion follows ARCHITECTURE.md: (x, y, z) -> (x, z, -y).
//! All constructors reject non-finite inputs. No unsafe code is allowed.
#![forbid(unsafe_code)]

use std::ops::{Add, Mul, Sub};

/// All 3D coordinates in this crate use meters and finite f64 values.
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Vec3 {
    pub x: f64,
    pub y: f64,
    pub z: f64,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum MathError {
    NonFinite,
    NonPositiveTolerance,
    ZeroLength,
}

impl Vec3 {
    /// Construct a finite, meter-based vector.
    pub fn new(x: f64, y: f64, z: f64) -> Result<Self, MathError> {
        if !x.is_finite() || !y.is_finite() || !z.is_finite() {
            return Err(MathError::NonFinite);
        }
        Ok(Self { x, y, z })
    }

    pub const ZERO: Self = Self { x: 0.0, y: 0.0, z: 0.0 };

    pub fn dot(self, rhs: Self) -> f64 {
        self.x.mul_add(rhs.x, self.y.mul_add(rhs.y, self.z * rhs.z))
    }

    pub fn cross(self, rhs: Self) -> Self {
        Self {
            x: self.y * rhs.z - self.z * rhs.y,
            y: self.z * rhs.x - self.x * rhs.z,
            z: self.x * rhs.y - self.y * rhs.x,
        }
    }

    pub fn length(self) -> f64 {
        self.x.hypot(self.y).hypot(self.z)
    }

    pub fn normalized(self) -> Result<Self, MathError> {
        let magnitude = self.length();
        if magnitude == 0.0 {
            return Err(MathError::ZeroLength);
        }
        if !magnitude.is_finite() {
            return Err(MathError::NonFinite);
        }
        Self::new(self.x / magnitude, self.y / magnitude, self.z / magnitude)
    }

    /// Proper rotation into WPF's Y-up coordinates (determinant +1).
    pub fn to_wpf(self) -> Self {
        Self { x: self.x, y: self.z, z: -self.y }
    }

    /// Inverse of `to_wpf`.
    pub fn from_wpf(self) -> Self {
        Self { x: self.x, y: -self.z, z: self.y }
    }

    pub fn approx_eq(self, rhs: Self, tolerance_m: f64) -> Result<bool, MathError> {
        if !tolerance_m.is_finite() || tolerance_m <= 0.0 {
            return Err(MathError::NonPositiveTolerance);
        }
        Ok((self - rhs).length() <= tolerance_m)
    }
}

impl Add for Vec3 {
    type Output = Self;
    fn add(self, rhs: Self) -> Self {
        Self { x: self.x + rhs.x, y: self.y + rhs.y, z: self.z + rhs.z }
    }
}
impl Sub for Vec3 {
    type Output = Self;
    fn sub(self, rhs: Self) -> Self {
        Self { x: self.x - rhs.x, y: self.y - rhs.y, z: self.z - rhs.z }
    }
}
impl Mul<f64> for Vec3 {
    type Output = Self;
    fn mul(self, scale: f64) -> Self {
        Self { x: self.x * scale, y: self.y * scale, z: self.z * scale }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn rejects_non_finite_coordinates() {
        assert_eq!(Vec3::new(f64::NAN, 0.0, 0.0), Err(MathError::NonFinite));
        assert_eq!(Vec3::new(0.0, f64::INFINITY, 0.0), Err(MathError::NonFinite));
    }

    #[test]
    fn converts_wpf_coordinates_and_back() {
        let original = Vec3::new(2.0, 3.0, 4.0).unwrap();
        assert_eq!(original.to_wpf(), Vec3::new(2.0, 4.0, -3.0).unwrap());
        assert_eq!(original.to_wpf().from_wpf(), original);
    }

    #[test]
    fn rotation_preserves_length_and_orientation() {
        let x = Vec3::new(1.0, 0.0, 0.0).unwrap();
        let y = Vec3::new(0.0, 1.0, 0.0).unwrap();
        let z = Vec3::new(0.0, 0.0, 1.0).unwrap();
        assert_eq!(x.cross(y), z);
        assert_eq!(x.to_wpf().cross(y.to_wpf()), z.to_wpf());
        assert_eq!(Vec3::new(3.0, 4.0, 12.0).unwrap().length(), 13.0);
    }

    #[test]
    fn normalized_and_tolerances() {
        assert_eq!(Vec3::ZERO.normalized(), Err(MathError::ZeroLength));
        let unit = Vec3::new(0.0, 3.0, 4.0).unwrap().normalized().unwrap();
        assert!((unit.length() - 1.0).abs() < 1e-12);
        assert_eq!(unit.approx_eq(unit, 0.0), Err(MathError::NonPositiveTolerance));
        assert!(unit.approx_eq(unit, 1e-9).unwrap());
    }
}
