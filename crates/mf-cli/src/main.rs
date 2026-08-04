mod commands;

use clap::{Parser, Subcommand};

#[derive(Parser)]
#[command(name = "mf", about = "Mate Framework CLI", version)]
struct Cli {
    #[command(subcommand)]
    command: Commands,

    /// Output in JSON format
    #[arg(long, global = true)]
    json: bool,
}

#[derive(Subcommand)]
enum Commands {
    /// Create a new Mate Framework project
    New {
        /// Project name
        name: String,
    },
    /// Diagnose issues with the current project
    Doctor,
    /// Start the development server
    Dev,
}

fn main() -> anyhow::Result<()> {
    let cli = Cli::parse();

    match cli.command {
        Commands::New { name } => commands::new::run(&name, cli.json),
        Commands::Doctor => commands::doctor::run(cli.json),
        Commands::Dev => commands::dev::run(cli.json),
    }
}