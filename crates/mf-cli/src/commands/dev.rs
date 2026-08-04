pub fn run(json: bool) -> anyhow::Result<()> {
    if json {
        println!(
            "{}",
            serde_json::json!({
                "status": "not_implemented",
                "message": "dev server will be implemented in Phase 1"
            })
        );
    } else {
        println!("`mf dev` will be implemented in Phase 1.");
        println!("This will launch the Mate Runtime against the current project.");
    }
    Ok(())
}
