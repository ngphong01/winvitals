using System.Diagnostics;

namespace App.Core;

public static class VscodeHelper
{
    public static bool IsCodeAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo("cmd", "/c code --version")
            {
                CreateNoWindow = true, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }

    public static HashSet<string> GetInstalledExtensions()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var psi = new ProcessStartInfo("cmd", "/c code --list-extensions")
            {
                CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true
            };
            using var p = Process.Start(psi);
            var output = p?.StandardOutput.ReadToEnd();
            p?.WaitForExit(5000);
            if (!string.IsNullOrEmpty(output))
                foreach (var line in output.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
                {
                    var id = line.Trim();
                    if (!string.IsNullOrEmpty(id)) set.Add(id);
                }
        }
        catch { }
        return set;
    }

    public static async Task<bool> InstallAsync(string extensionId)
    {
        try
        {
            var psi = new ProcessStartInfo("cmd", $"/c code --install-extension {extensionId} --force")
            {
                CreateNoWindow = true, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            if (p == null) return false;
            await p.WaitForExitAsync();
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    public static readonly (string Id, string Name, string Desc, string Category)[] Recommended =
    [
        // ── .NET / C# ──
        ("ms-dotnettools.csdevkit", "C# Dev Kit", "C# development — IntelliSense, debugging, solution management", ".NET"),
        ("ms-dotnettools.vscode-dotnet-runtime", ".NET Runtime", "Required by C# Dev Kit for debugging", ".NET"),
        ("ms-dotnettools.csharp", "C#", "Base C# language support (Roslyn)", ".NET"),
        ("kreativ-software.csharpextensions", "C# Extensions", "Quick C# class/interface generation", ".NET"),
        ("adrianwilczynski.asp-net-core-snippet", "ASP.NET Core Snippets", "Snippets for ASP.NET Core controllers/views", ".NET"),
        ("adrianwilczynski.add-reference", "Add Reference", "Add NuGet/project references via UI", ".NET"),

        // ── Git ──
        ("eamodio.gitlens", "GitLens", "Supercharge Git — blame, history, comparisons, code lens", "Git"),
        ("mhutchie.git-graph", "Git Graph", "Visual Git branch/commit graph", "Git"),
        ("donjayamanne.githistory", "Git History", "View file/line git history, diffs & compare branches", "Git"),
        ("github.vscode-github-actions", "GitHub Actions", "CI/CD workflow syntax, validation, run monitoring", "Git"),
        ("github.vscode-pull-request-github", "GitHub PR", "Review & manage GitHub pull requests in VS Code", "Git"),
        ("eamodio.gitlens-inspect", "GitLens Inspect", "Deep repository analysis & visualization", "Git"),

        // ── AI ──
        ("github.copilot", "GitHub Copilot", "AI pair programmer — code completions & chat", "AI"),
        ("github.copilot-chat", "Copilot Chat", "AI chat in VS Code sidebar", "AI"),
        ("tabnine.tabnine-vscode", "Tabnine", "AI code completions (alternative to Copilot)", "AI"),
        ("codeium.codeium", "Codeium", "Free AI code autocomplete & search", "AI"),

        // ── Web / Frontend ──
        ("dbaeumer.vscode-eslint", "ESLint", "JavaScript/TypeScript linting", "Web"),
        ("esbenp.prettier-vscode", "Prettier", "Code formatter for JS/TS/CSS/HTML/JSON/MD", "Web"),
        ("bradlc.vscode-tailwindcss", "Tailwind CSS", "IntelliSense, linting, class sorting for Tailwind", "Web"),
        ("vue.volar", "Vue — Official", "Vue 3 language support (formerly Volar)", "Web"),
        ("ms-vscode.vscode-typescript-next", "TypeScript 5.7+", "Latest TypeScript language features", "Web"),
        ("angular.ng-template", "Angular", "Angular template language service", "Web"),
        ("svelte.svelte-vscode", "Svelte", "Svelte language support & diagnostics", "Web"),
        ("ritwickdey.liveserver", "Live Server", "Local dev server with live reload for static/dynamic pages", "Web"),
        ("formulahendry.auto-rename-tag", "Auto Rename Tag", "Auto rename paired HTML/XML tags", "Web"),
        ("naumovs.color-highlight", "Color Highlight", "Highlight CSS/web colors in editor", "Web"),
        ("pranaygp.vscode-css-peek", "CSS Peek", "Peek at CSS definitions from HTML", "Web"),
        ("christian-kohler.path-intellisense", "Path IntelliSense", "Autocomplete filenames & paths", "Web"),
        ("ecmel.vscode-html-css", "HTML CSS Support", "CSS IntelliSense for HTML class/id", "Web"),
        ("styled-components.vscode-styled-components", "Styled Components", "CSS-in-JS syntax highlighting & IntelliSense", "Web"),
        ("zignd.html-css-class-completion", "CSS Class Completion", "CSS class name completion in HTML", "Web"),

        // ── Python ──
        ("ms-python.python", "Python", "Python IntelliSense, debugging, testing", "Python"),
        ("ms-python.debugpy", "Python Debugger", "Python debugging support", "Python"),
        ("ms-python.black-formatter", "Black Formatter", "Python code formatter (Black)", "Python"),
        ("ms-python.isort", "isort", "Python import sorting", "Python"),
        ("ms-python.pylint", "Pylint", "Python linting & static analysis", "Python"),
        ("ms-python.autopep8", "autopep8", "Python PEP8 formatter", "Python"),
        ("kevinrose.vsc-python-indent", "Python Indent", "Smart Python indentation", "Python"),

        // ── Rust ──
        ("rust-lang.rust-analyzer", "rust-analyzer", "Rust IntelliSense, cargo, clippy, debugging", "Rust"),
        ("tamasfe.even-better-toml", "Better TOML", "TOML syntax highlighting for Cargo.toml", "Rust"),
        ("vadimcn.vscode-lldb", "CodeLLDB", "LLDB debugger for Rust/C/C++", "Rust"),
        ("serayuzgur.crates", "crates", "Cargo.toml dependency version checker", "Rust"),

        // ── Go ──
        ("golang.go", "Go", "Go IntelliSense, debugging, testing, formatting", "Go"),
        ("ms-vscode.go-nightly", "Go Nightly", "Nightly Go language features", "Go"),

        // ── JVM ──
        ("redhat.java", "Java", "Java LSP — IntelliSense, debugging, Maven/Gradle", "JVM"),
        ("vscjava.vscode-gradle", "Gradle", "Gradle build tool integration", "JVM"),
        ("vscjava.vscode-maven", "Maven", "Maven build tool integration", "JVM"),
        ("vscjava.vscode-java-debug", "Java Debug", "Java debugging support", "JVM"),
        ("vscjava.vscode-java-test", "Java Test", "JUnit/TestNG test runner", "JVM"),
        ("vscjava.vscode-spring-initializr", "Spring Boot", "Spring Boot project generator & support", "JVM"),
        ("vmware.vscode-boot-dev-pack", "Spring Boot Pack", "Spring Boot extension pack", "JVM"),
        ("gabrielbb.vscode-lombok", "Lombok", "Java Lombok annotation support", "JVM"),
        ("fwcd.kotlin", "Kotlin", "Kotlin language support", "JVM"),
        ("scala-lang.scala", "Scala (Metals)", "Scala language support via Metals LSP", "JVM"),

        // ── Mobile ──
        ("dart-code.flutter", "Flutter", "Flutter & Dart — hot reload, widget inspector", "Mobile"),
        ("dart-code.dart-code", "Dart", "Dart language support", "Mobile"),
        ("nash.awesome-flutter-snippets", "Flutter Snippets", "Flutter widget snippets", "Mobile"),

        // ── DevOps / Cloud ──
        ("ms-azuretools.vscode-docker", "Docker", "Dockerfile, compose, container explorer, image management", "DevOps"),
        ("ms-vscode-remote.remote-containers", "Dev Containers", "Develop inside Docker containers", "DevOps"),
        ("ms-kubernetes-tools.vscode-kubernetes-tools", "Kubernetes", "K8s resource management, YAML validation, helm", "DevOps"),
        ("ms-vscode.powershell", "PowerShell", "PowerShell language support, debugging, ISE mode", "DevOps"),
        ("hashicorp.terraform", "Terraform", "HCL syntax, validation, plan/apply integration", "DevOps"),
        ("ms-azuretools.vscode-bicep", "Bicep", "Azure Bicep IaC language support", "DevOps"),
        ("amazonwebservices.aws-toolkit-vscode", "AWS Toolkit", "AWS services, Lambda, S3, CloudFormation", "DevOps"),
        ("googlecloudtools.cloudcode", "Cloud Code", "Google Cloud — Kubernetes, Cloud Run, GKE", "DevOps"),
        ("redhat.vscode-yaml", "YAML", "YAML validation, schema, autocomplete, Kubernetes schema", "DevOps"),
        ("ms-vscode.remote-explorer", "Remote Explorer", "Browse remote SSH/container/WSL targets", "DevOps"),
        ("ms-vscode-remote.remote-ssh", "Remote SSH", "Edit code over SSH connection", "DevOps"),
        ("ms-vscode-remote.remote-wsl", "Remote WSL", "Edit code in Windows Subsystem for Linux", "DevOps"),

        // ── Databases ──
        ("mtxr.sqltools", "SQL Tools", "Database explorer & query runner (MySQL, PG, SQLite, MSSQL)", "Database"),
        ("cweijan.vscode-mysql-client2", "MySQL Client", "MySQL/MariaDB database manager", "Database"),
        ("ms-mssql.mssql", "MSSQL", "Microsoft SQL Server support", "Database"),
        ("mongodb.mongodb-vscode", "MongoDB", "MongoDB playgrounds, queries, explorer", "Database"),
        ("ms-ossdata.vscode-postgresql", "PostgreSQL", "PostgreSQL query & management", "Database"),
        ("redis.redis-for-vscode", "Redis", "Redis data viewer & query runner", "Database"),

        // ── Testing ──
        ("ms-playwright.playwright", "Playwright Test", "Run Playwright tests, debug, record, inspect", "Testing"),
        ("orta.vscode-jest", "Jest", "Jest test runner with inline results", "Testing"),
        ("hbenl.vscode-test-explorer", "Test Explorer", "Universal test explorer UI", "Testing"),
        ("ms-vscode.test-adapter-converter", "Test Adapter", "Convert test adapters for Test Explorer", "Testing"),
        ("andys8.jest-snippets", "Jest Snippets", "Jest code snippets", "Testing"),

        // ── API ──
        ("rangav.vscode-thunder-client", "Thunder Client", "Lightweight REST API client (like Postman)", "API"),
        ("humao.rest-client", "REST Client", "Send HTTP requests from .http files", "API"),
        ("42crunch.vscode-openapi", "OpenAPI", "OpenAPI/Swagger editor & security audit", "API"),
        ("mermade.openapi-lint", "OpenAPI Lint", "OpenAPI spec linting & validation", "API"),

        // ── Docs ──
        ("yzhang.markdown-all-in-one", "Markdown All-in-One", "TOC, preview, formatting, list editing", "Docs"),
        ("bierner.markdown-mermaid", "Mermaid", "Diagram rendering in Markdown preview", "Docs"),
        ("streetsidesoftware.code-spell-checker", "Code Spell Checker", "Spell checking for code and docs (multi-language)", "Docs"),
        ("editorconfig.editorconfig", "EditorConfig", "Consistent coding styles across editors", "Docs"),
        ("mikestead.dotenv", "DotENV", "Syntax highlighting for .env files", "Docs"),
        ("davidanson.vscode-markdownlint", "markdownlint", "Markdown linting and style checking", "Docs"),
        ("mushan.vscode-paste-image", "Paste Image", "Paste clipboard image directly into Markdown", "Docs"),

        // ── Theme / Visual ──
        ("pkief.material-icon-theme", "Material Icons", "Beautiful file & folder icons", "Theme"),
        ("vscode-icons-team.vscode-icons", "vscode-icons", "Alternative file icon theme", "Theme"),
        ("zhuangtongfa.material-theme", "One Dark Pro", "Popular dark theme — Atom One Dark for VS Code", "Theme"),
        ("dracula-theme.theme-dracula", "Dracula", "Dark theme with purple/blue tones", "Theme"),
        ("johnpapa.vscode-peacock", "Peacock", "Color-code workspace windows per project", "Theme"),
        ("antfu.icons-carbon", "Carbon Icons", "Carbon Design System icon theme", "Theme"),
        ("antfu.theme-vitesse", "Vitesse Theme", "Clean, minimal dark/light theme", "Theme"),

        // ── Quality / Productivity ──
        ("usernamehw.errorlens", "Error Lens", "Inline error/warning messages in code", "Quality"),
        ("oderwat.indent-rainbow", "Indent Rainbow", "Colorize indentation levels", "Quality"),
        ("wayou.vscode-todo-highlight", "TODO Highlight", "Highlight TODO/FIXME/HACK comments", "Quality"),
        ("gruntfuggly.todo-tree", "Todo Tree", "Aggregate TODO/FIXME into tree view", "Quality"),
        ("aaron-bond.better-comments", "Better Comments", "Color-coded human-friendly comments", "Quality"),
        ("alefragnani.bookmarks", "Bookmarks", "Mark & navigate code bookmarks", "Quality"),
        ("wix.vscode-import-cost", "Import Cost", "Show import/require package size inline", "Quality"),
        ("github.vscode-codeql", "CodeQL", "Semantic code analysis & security scanning", "Quality"),
        ("sonarsource.sonarlint-vscode", "SonarLint", "Real-time code quality & security issues", "Quality"),
        ("mechatroner.rainbow-csv", "Rainbow CSV", "Colorize CSV/TSV columns for readability", "Quality"),
        ("tabnine.tabnine-vscode", "Tabnine", "AI code completions (alternative)", "Quality"),
        ("visualstudioexptteam.vscodeintellicode", "IntelliCode", "AI-assisted IntelliSense completions", "Quality"),
        ("shd101wyy.markdown-preview-enhanced", "Markdown Preview", "Enhanced Markdown preview with TOC, diagrams", "Quality"),
        ("formulahendry.code-runner", "Code Runner", "Run code snippets in 50+ languages", "Quality"),
        ("ms-vscode.live-server", "Live Preview", "Live HTML preview with auto-refresh", "Quality"),
        ("eamodio.toggle-excluded-files", "Toggle Excluded", "Quickly toggle excluded/hidden files in explorer", "Quality"),
        ("adrianwilczynski.toggle-hidden", "Toggle Hidden", "Toggle hidden files visibility", "Quality"),

        // ── C/C++ ──
        ("ms-vscode.cpptools", "C/C++", "C/C++ IntelliSense, debugging, code browsing", "C/C++"),
        ("ms-vscode.cmake-tools", "CMake Tools", "CMake build system support & configure", "C/C++"),
        ("twxs.cmake", "CMake", "CMake language syntax highlighting", "C/C++"),
        ("ms-vscode.makefile-tools", "Makefile Tools", "Makefile build support & IntelliSense", "C/C++"),

        // ── PHP ──
        ("bmewburn.vscode-intelephense-client", "Intelephense", "PHP IntelliSense, refactoring, code analysis", "PHP"),
        ("xdebug.php-debug", "PHP Debug", "PHP Xdebug debugging support", "PHP"),
        ("neilbrayfield.php-docblocker", "PHP DocBlocker", "Quick PHP docblock generation", "PHP"),
        ("ikappas.phpcs", "PHPCS", "PHP CodeSniffer — coding standards enforcement", "PHP"),

        // ── Ruby / Rails ──
        ("shopify.ruby-lsp", "Ruby LSP", "Ruby language support & IntelliSense", "Ruby"),
        ("castwide.solargraph", "Solargraph", "Ruby IntelliSense & documentation", "Ruby"),
        ("wingrunr21.vscode-ruby", "Ruby", "Ruby syntax & language support", "Ruby"),

        // ── Data / Notebook ──
        ("ms-toolsai.jupyter", "Jupyter", "Jupyter notebook support in VS Code", "Data"),
        ("ms-toolsai.datawrangler", "Data Wrangler", "Data cleaning & preparation tool", "Data"),
        ("grapecity.gc-excelviewer", "Excel Viewer", "View CSV/Excel files in VS Code", "Data"),
        ("mechatroner.rainbow-csv", "Rainbow CSV", "Colorize CSV columns", "Data"),
    ];
}
