//! High-performance native physics & fluid animation engine for Flow Launcher.
//! Provides analytical spring-damper physics, smooth fluid easing curves,
//! and high-framerate trajectory generators.

use std::f64::consts::PI;

/// Curve types supported by the native animation evaluator.
#[repr(u32)]
#[derive(Copy, Clone, Debug, PartialEq, Eq)]
pub enum CurveType {
    EaseInOutCubic = 0,
    EaseOutQuint = 1,
    EaseOutExpo = 2,
    SpringFluid = 3,
    FluidGlassRefract = 4,
    FastOutSlowIn = 5,
}

/// Evaluates an easing or physics curve at normalized progress `t` in [0.0, 1.0].
#[no_mangle]
pub extern "C" fn flow_animation_eval_curve(curve_type: u32, t: f64) -> f64 {
    let t = t.clamp(0.0, 1.0);
    match curve_type {
        // EaseInOutCubic
        0 => {
            if t < 0.5 {
                4.0 * t * t * t
            } else {
                1.0 - (-2.0 * t + 2.0).powi(3) / 2.0
            }
        }
        // EaseOutQuint
        1 => 1.0 - (1.0 - t).powi(5),
        // EaseOutExpo
        2 => {
            if (t - 1.0).abs() < f64::EPSILON {
                1.0
            } else {
                1.0 - 2.0f64.powf(-10.0 * t)
            }
        }
        // SpringFluid (Damped harmonic spring with subtle fluid overshoot)
        3 => {
            let omega_n: f64 = 10.0;
            let zeta: f64 = 0.72; // slight underdamping for pleasant fluid snap
            let omega_d: f64 = omega_n * (1.0 - zeta * zeta).sqrt();
            let decay = (-zeta * omega_n * t).exp();
            1.0 - decay * ((omega_d * t).cos() + (zeta / (1.0 - zeta * zeta).sqrt()) * (omega_d * t).sin())
        }
        // FluidGlassRefract (Specular fluid curve with rapid onset and smooth viscous settle)
        4 => {
            let p = 1.0 - (1.0 - t).powi(4);
            let ripple = (t * PI * 2.0).sin() * 0.04 * (1.0 - t);
            (p + ripple).clamp(0.0, 1.0)
        }
        // FastOutSlowIn (Standard Fluent / Material 3 fluid decelerate)
        5 => {
            // Cubic bezier approximation (0.05, 0.7, 0.1, 1.0)
            let c = 1.0 - t;
            1.0 - c * c * c
        }
        _ => t,
    }
}

/// Computes a single integration step for a spring-damper system.
/// `current`: current position
/// `target`: destination position
/// `velocity`: pointer to in-out velocity
/// `stiffness`: spring stiffness constant (e.g. 180.0)
/// `damping`: damping coefficient (e.g. 24.0)
/// `dt`: elapsed seconds (e.g. 1.0 / 120.0)
#[no_mangle]
pub unsafe extern "C" fn flow_animation_spring_step(
    current: f64,
    target: f64,
    velocity: *mut f64,
    stiffness: f64,
    damping: f64,
    dt: f64,
) -> f64 {
    if velocity.is_null() || dt <= 0.0 {
        return target;
    }

    let vel = *velocity;
    let displacement = current - target;
    let spring_force = -stiffness * displacement;
    let damping_force = -damping * vel;
    let acceleration = spring_force + damping_force; // assuming mass = 1.0

    let new_vel = vel + acceleration * dt;
    let new_pos = current + new_vel * dt;

    *velocity = new_vel;
    new_pos
}

/// Generates a pre-computed array of analytical spring-damper keyframes
/// sampled uniformly at the specified framerate (e.g. 60, 120, 240 fps).
/// Returns 1 on success, 0 on failure.
#[no_mangle]
pub unsafe extern "C" fn flow_animation_generate_spring_keyframes(
    from: f64,
    to: f64,
    duration_ms: u32,
    fps: u32,
    stiffness: f64,
    damping_ratio: f64,
    out_buffer: *mut f64,
    max_frames: usize,
    out_count: *mut usize,
) -> i32 {
    if out_buffer.is_null() || out_count.is_null() || duration_ms == 0 || fps == 0 || max_frames == 0 {
        return 0;
    }

    let fps = fps.clamp(30, 240);
    let total_seconds = (duration_ms as f64) / 1000.0;
    let frame_count = ((total_seconds * fps as f64).round() as usize).max(2).min(max_frames);
    let dt = total_seconds / (frame_count - 1) as f64;

    // Natural frequency derived from stiffness (mass = 1.0)
    let omega_n = stiffness.max(1.0).sqrt();
    let zeta = damping_ratio.clamp(0.01, 2.0);

    let x0 = from - to;
    let v0 = 0.0f64;

    for i in 0..frame_count {
        let t = i as f64 * dt;
        let pos = if (zeta - 1.0).abs() < 1e-4 {
            // Critically damped
            let c1 = x0;
            let c2 = v0 + omega_n * x0;
            (-omega_n * t).exp() * (c1 + c2 * t)
        } else if zeta < 1.0 {
            // Underdamped
            let omega_d = omega_n * (1.0 - zeta * zeta).sqrt();
            let c1 = x0;
            let c2 = (v0 + zeta * omega_n * x0) / omega_d;
            let decay = (-zeta * omega_n * t).exp();
            decay * (c1 * (omega_d * t).cos() + c2 * (omega_d * t).sin())
        } else {
            // Overdamped
            let r1 = -omega_n * (zeta - (zeta * zeta - 1.0).sqrt());
            let r2 = -omega_n * (zeta + (zeta * zeta - 1.0).sqrt());
            let c2 = (v0 - r1 * x0) / (r2 - r1);
            let c1 = x0 - c2;
            c1 * (r1 * t).exp() + c2 * (r2 * t).exp()
        };

        *out_buffer.add(i) = to + pos;
    }

    // Force final keyframe to exact destination
    *out_buffer.add(frame_count - 1) = to;
    *out_count = frame_count;
    1
}
