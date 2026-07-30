using Hexara.Infrastructure.Persistence.Migrations.Data;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hexara.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// رنگ آواتار کاربران موجود را از پالت قدیمی (فیروزه‌ای/آبیِ نشانِ قبل از
    /// بازطراحی تم) به پالت دنیای پارشمنت می‌برد.
    ///
    /// نگاشت جای‌به‌جا است و تصادفی نیست: هر رنگ به هم‌خانواده‌ی خودش می‌رود، پس
    /// کسی که آبی بود آبی می‌ماند. هویت رنگی آدم‌ها چیزی است که به آن عادت
    /// کرده‌اند و بی‌دلیل عوض کردنش آزاردهنده است.
    /// </summary>
    public partial class RepaintAvatarColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            Repaint(migrationBuilder, forward: true);

        /// <summary>
        /// برگشت‌پذیر است تا اگر استقرار عقب برگردد، رنگ‌ها هم با کدِ قدیمی بخوانند.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder) =>
            Repaint(migrationBuilder, forward: false);

        /// <summary>
        /// رنگ‌هایی که در نگاشت نیستند دست‌نخورده می‌مانند. عمدی است: تنها مقادیرِ
        /// ممکن همان پالت قبلی و پیش‌فرضش است، و اگر روزی کاربری رنگ دلخواه
        /// انتخاب کند نباید مهاجرت آن را پاک کند.
        /// </summary>
        private static void Repaint(MigrationBuilder migrationBuilder, bool forward)
        {
            foreach (var statement in AvatarRepaint.Statements(forward))
            {
                migrationBuilder.Sql(statement);
            }
        }
    }
}
