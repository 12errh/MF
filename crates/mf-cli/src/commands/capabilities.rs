use std::env;

pub fn run(json: bool) -> anyhow::Result<()> {
    let desktop = env::var("XDG_CURRENT_DESKTOP").unwrap_or_default();
    let session = env::var("XDG_SESSION_TYPE").unwrap_or_default();
    let hyprland = env::var("HYPRLAND_INSTANCE_SIGNATURE").is_ok();
    let wayland = session.contains("wayland");

    let capabilities = serde_json::json!({
        "platform": {
            "desktop_environment": desktop,
            "session_type": session,
            "is_wayland": wayland,
            "is_hyprland": hyprland,
        },
        "features": {
            "transparency": !wayland || hyprland,
            "click_through": session == "x11" || hyprland,
            "always_on_top": true,
            "system_tray": !wayland || session == "x11",
            "notifications": true,
            "audio_monitoring": session == "x11",
        },
        "runtime_required": "Unity 6000.2.6f2",
        "cli_version": env!("CARGO_PKG_VERSION"),
    });

    if json {
        println!("{}", serde_json::to_string_pretty(&capabilities)?);
    } else {
        println!("Platform capabilities:");
        println!("  Desktop: {desktop}");
        println!("  Session: {session}");
        println!(
            "  Transparency: {}",
            capabilities["features"]["transparency"]
        );
        println!(
            "  Click-through: {}",
            capabilities["features"]["click_through"]
        );
        println!(
            "  Always-on-top: {}",
            capabilities["features"]["always_on_top"]
        );
        println!("  System tray: {}", capabilities["features"]["system_tray"]);
        println!(
            "  Notifications: {}",
            capabilities["features"]["notifications"]
        );
        println!(
            "  Audio monitoring: {}",
            capabilities["features"]["audio_monitoring"]
        );
    }

    Ok(())
}
