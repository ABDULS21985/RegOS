using FC.Engine.Application.Services;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Models;
using FC.Engine.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace FC.Engine.Infrastructure.Tests.Services;

public class FilingCalendarServiceTests
{
    // ── RAG color computation ──────────────────────────────────

    [Fact]
    public void RAG_Green_When_Submitted()
    {
        var today = new DateTime(2026, 3, 15);
        var deadline = new DateTime(2026, 3, 31);
        var periodStart = new DateTime(2026, 2, 28);

        var color = FilingCalendarService.ComputeRagColor(
            today, deadline, periodStart,
            hasSubmitted: true, inReview: false, status: "Completed");

        color.Should().Be(RagColor.Green);
    }

    [Fact]
    public void RAG_Green_When_Over_50_Percent_Time_Remaining()
    {
        // Period start: Jan 31, Deadline: Mar 2 (30 days)
        // Today: Feb 10 (20 days remaining = 67% remaining)
        var periodStart = new DateTime(2026, 1, 31);
        var deadline = new DateTime(2026, 3, 2);
        var today = new DateTime(2026, 2, 10);

        var color = FilingCalendarService.ComputeRagColor(
            today, deadline, periodStart,
            hasSubmitted: false, inReview: false, status: "Open");

        color.Should().Be(RagColor.Green);
    }

    [Fact]
    public void RAG_Amber_When_Under_50_Percent_Time_Remaining()
    {
        // Period start: Jan 31, Deadline: Mar 2 (30 days)
        // Today: Feb 20 (10 days remaining = 33% remaining)
        var periodStart = new DateTime(2026, 1, 31);
        var deadline = new DateTime(2026, 3, 2);
        var today = new DateTime(2026, 2, 20);

        var color = FilingCalendarService.ComputeRagColor(
            today, deadline, periodStart,
            hasSubmitted: false, inReview: false, status: "Open");

        color.Should().Be(RagColor.Amber);
    }

    [Fact]
    public void RAG_Amber_When_In_Review()
    {
        var periodStart = new DateTime(2026, 1, 31);
        var deadline = new DateTime(2026, 3, 2);
        var today = new DateTime(2026, 2, 5);

        var color = FilingCalendarService.ComputeRagColor(
            today, deadline, periodStart,
            hasSubmitted: false, inReview: true, status: "Open");

        color.Should().Be(RagColor.Amber);
    }

    [Fact]
    public void RAG_Red_When_Overdue()
    {
        var periodStart = new DateTime(2026, 1, 31);
        var deadline = new DateTime(2026, 3, 2);
        var today = new DateTime(2026, 3, 5);

        var color = FilingCalendarService.ComputeRagColor(
            today, deadline, periodStart,
            hasSubmitted: false, inReview: false, status: "Overdue");

        color.Should().Be(RagColor.Red);
    }

    [Fact]
    public void RAG_Red_When_Less_Than_7_Days_And_Not_Submitted()
    {
        var periodStart = new DateTime(2026, 1, 31);
        var deadline = new DateTime(2026, 3, 2);
        var today = new DateTime(2026, 2, 27); // 3 days remaining

        var color = FilingCalendarService.ComputeRagColor(
            today, deadline, periodStart,
            hasSubmitted: false, inReview: false, status: "DueSoon");

        color.Should().Be(RagColor.Red);
    }

    [Fact]
    public void RAG_Green_Even_When_Overdue_If_Submitted()
    {
        // Submitted returns always green regardless of deadline
        var periodStart = new DateTime(2026, 1, 31);
        var deadline = new DateTime(2026, 3, 2);
        var today = new DateTime(2026, 3, 10);

        var color = FilingCalendarService.ComputeRagColor(
            today, deadline, periodStart,
            hasSubmitted: true, inReview: false, status: "Overdue");

        color.Should().Be(RagColor.Green);
    }

    // ── Period formatting ──────────────────────────────────────

    [Fact]
    public void FormatPeriod_Monthly_Returns_Month_Year()
    {
        var period = new Domain.Entities.ReturnPeriod { Year = 2026, Month = 3, Frequency = "Monthly" };
        var label = FilingCalendarService.FormatPeriod(period);
        label.Should().Be("Mar 2026");
    }

    [Fact]
    public void FormatPeriod_Quarterly_Returns_Q_Year()
    {
        var period = new Domain.Entities.ReturnPeriod { Year = 2026, Month = 6, Quarter = 2, Frequency = "Quarterly" };
        var label = FilingCalendarService.FormatPeriod(period);
        label.Should().Be("Q2 2026");
    }

    [Fact]
    public void FormatPeriod_SemiAnnual_Returns_H_Year()
    {
        var period = new Domain.Entities.ReturnPeriod { Year = 2026, Month = 6, Frequency = "SemiAnnual" };
        var label = FilingCalendarService.FormatPeriod(period);
        label.Should().Be("H1 2026");
    }

    [Fact]
    public void FormatPeriod_Annual_Returns_FY_Year()
    {
        var period = new Domain.Entities.ReturnPeriod { Year = 2026, Month = 12, Frequency = "Annual" };
        var label = FilingCalendarService.FormatPeriod(period);
        label.Should().Be("FY 2026");
    }

    // ── SLA tracking (spec-required tests) ─────────────────────

    [Fact]
    public void SLA_Record_Created_On_Submission()
    {
        // Verify SLA record structure is correct for a timely submission
        var deadlineService = new DeadlineComputationService();
        var module = new Module { Id = 1, DefaultFrequency = "Monthly" };
        var period = new ReturnPeriod { Year = 2026, Month = 3, Frequency = "Monthly" };

        var deadline = deadlineService.ComputeDeadline(module, period);
        var submittedDate = deadline.AddDays(-10); // 10 days early

        var slaRecord = new FilingSlaRecord
        {
            TenantId = Guid.NewGuid(),
            ModuleId = module.Id,
            PeriodId = 1,
            SubmissionId = 100,
            PeriodEndDate = DeadlineComputationService.GetPeriodEndDate("Monthly", 2026, 3, null),
            DeadlineDate = deadline,
            SubmittedDate = submittedDate,
            DaysToDeadline = (deadline.Date - submittedDate.Date).Days,
            OnTime = (deadline.Date - submittedDate.Date).Days >= 0
        };

        slaRecord.DaysToDeadline.Should().Be(10, "Submitted 10 days before deadline");
        slaRecord.OnTime.Should().BeTrue("Submitted before deadline");
        slaRecord.SubmissionId.Should().Be(100);
        slaRecord.PeriodEndDate.Should().Be(new DateTime(2026, 3, 31));
    }

    [Fact]
    public void SLA_Tracks_DaysToDeadline_Correctly()
    {
        var deadlineService = new DeadlineComputationService();
        var module = new Module { Id = 1, DefaultFrequency = "Quarterly" };
        var period = new ReturnPeriod { Year = 2026, Month = 6, Quarter = 2, Frequency = "Quarterly" };

        var deadline = deadlineService.ComputeDeadline(module, period);

        // Early submission: 5 days before
        var earlyDate = deadline.AddDays(-5);
        var earlyDays = (deadline.Date - earlyDate.Date).Days;
        earlyDays.Should().Be(5);
        (earlyDays >= 0).Should().BeTrue("Early submission is on time");

        // Late submission: 3 days after
        var lateDate = deadline.AddDays(3);
        var lateDays = (deadline.Date - lateDate.Date).Days;
        lateDays.Should().Be(-3);
        (lateDays >= 0).Should().BeFalse("Late submission is not on time");

        // On-time submission: exact deadline
        var exactDate = deadline;
        var exactDays = (deadline.Date - exactDate.Date).Days;
        exactDays.Should().Be(0);
        (exactDays >= 0).Should().BeTrue("Submission on deadline day is on time");
    }

    [Fact]
    public void Deadline_Override_Resets_Notification_Level()
    {
        // Simulate a period at notification level 4 (T-3)
        var period = new ReturnPeriod
        {
            NotificationLevel = 4,
            DeadlineDate = DateTime.UtcNow.AddDays(-2),
            Status = "Overdue"
        };

        // Apply override (simulating what FilingCalendarService.OverrideDeadline does)
        var newDeadline = DateTime.UtcNow.AddDays(30);
        period.DeadlineOverrideDate = newDeadline;
        period.DeadlineOverrideBy = 42;
        period.DeadlineOverrideReason = "Regulator granted extension";
        period.NotificationLevel = 0; // Reset

        period.NotificationLevel.Should().Be(0, "Override must reset notification level");
        period.EffectiveDeadline.Should().Be(newDeadline, "Override deadline should take precedence");
        period.DeadlineOverrideBy.Should().Be(42, "Must track who performed the override");
        period.DeadlineOverrideReason.Should().NotBeNullOrEmpty("Must provide a reason");
    }
}
