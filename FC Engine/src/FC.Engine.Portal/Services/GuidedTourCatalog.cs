namespace FC.Engine.Portal.Services;

public static class GuidedTourCatalog
{
    public static GuidedTourDefinition GetDefinition(string role)
    {
        return role.Trim().ToLowerInvariant() switch
        {
            "checker" => CheckerTour(),
            "admin" => AdminTour(),
            _ => MakerTour()
        };
    }

    private static GuidedTourDefinition MakerTour()
    {
        return new GuidedTourDefinition
        {
            Role = "Maker",
            Title = "Maker Guided Tour",
            Steps = new List<GuidedTourStepDefinition>
            {
                new()
                {
                    TargetSelector = "a[href='/']",
                    Title = "Dashboard",
                    Description = "Track deadlines, RAG status, and pending submissions from your home dashboard.",
                    Position = "right"
                },
                new()
                {
                    TargetSelector = "a[href='/calendar']",
                    Title = "Filing Calendar",
                    Description = "Review upcoming reporting periods and due dates by module.",
                    Position = "right"
                },
                new()
                {
                    TargetSelector = ".portal-nav-primary-submit, .portal-mobile-nav-primary-submit",
                    Title = "Create Return",
                    Description = "Start XML upload or manual form-based data entry for a return template.",
                    Position = "right"
                },
                new()
                {
                    TargetSelector = "a[href='/submissions']",
                    Title = "Track Progress",
                    Description = "Monitor validation outcomes and submission states for your returns.",
                    Position = "right"
                },
                new()
                {
                    TargetSelector = "a[href='/approvals']",
                    Title = "Submit for Review",
                    Description = "When maker-checker is enabled, hand off validated returns for checker review.",
                    Position = "right"
                }
            }
        };
    }

    private static GuidedTourDefinition CheckerTour()
    {
        return new GuidedTourDefinition
        {
            Role = "Checker",
            Title = "Checker Guided Tour",
            Steps = new List<GuidedTourStepDefinition>
            {
                new()
                {
                    TargetSelector = "a[href='/']",
                    Title = "Dashboard Signals",
                    Description = "Use dashboard indicators to prioritize overdue and high-risk filings.",
                    Position = "right"
                },
                new()
                {
                    TargetSelector = "a[href='/approvals']",
                    Title = "Pending Reviews",
                    Description = "Open approval queue to inspect submissions awaiting checker action.",
                    Position = "right"
                },
                new()
                {
                    TargetSelector = "a[href='/reports/audit']",
                    Title = "Audit Trail",
                    Description = "Validate who changed what before approving or rejecting a return.",
                    Position = "right"
                },
                new()
                {
                    TargetSelector = "a[href='/notifications']",
                    Title = "Respond Quickly",
                    Description = "Use notifications to follow reviewer queries and escalations.",
                    Position = "right"
                }
            }
        };
    }

    private static GuidedTourDefinition AdminTour()
    {
        return new GuidedTourDefinition
        {
            Role = "Admin",
            Title = "Admin Guided Tour",
            Steps = new List<GuidedTourStepDefinition>
            {
                new()
                {
                    TargetSelector = "a[href='/institution/team']",
                    Title = "User Management",
                    Description = "Create Maker, Checker, and Approver users and enforce role separation.",
                    Position = "right"
                },
                new()
                {
                    TargetSelector = "a[href='/subscription/my-plan']",
                    Title = "Subscription",
                    Description = "Monitor usage, plan limits, and renewals.",
                    Position = "right"
                },
                new()
                {
                    TargetSelector = "a[href='/subscription/modules']",
                    Title = "Module Activation",
                    Description = "Activate modules aligned to your licence type and reporting scope.",
                    Position = "right"
                },
                new()
                {
                    TargetSelector = "a[href='/settings/branding']",
                    Title = "Branding",
                    Description = "Apply institution branding and upload approved logos.",
                    Position = "right"
                },
                new()
                {
                    TargetSelector = "a[href='/reports/builder']",
                    Title = "Reports",
                    Description = "Create executive and compliance reports from current filing data.",
                    Position = "right"
                }
            }
        };
    }
}

public class GuidedTourDefinition
{
    public string Role { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<GuidedTourStepDefinition> Steps { get; set; } = new();
}

public class GuidedTourStepDefinition
{
    public string TargetSelector { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Position { get; set; } = "bottom";
}
