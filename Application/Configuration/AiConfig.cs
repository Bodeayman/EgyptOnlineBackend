namespace EgyptOnline.Application.Configuration
{
    /// <summary>
    /// Central configuration object for the AI assistant.
    /// Loaded from appsettings at startup and injected as a singleton.
    ///
    /// HOW TO EDIT COMPANY RULES / GUARDRAILS
    /// ----------------------------------------
    /// 1. Open appsettings.Development.json  (local dev)
    ///    or set environment variables        (production / Docker)
    ///    under the "AiAssistant" section.
    ///
    /// 2. The most important field is "Rules" — it is a list of plain-language
    ///    instructions that Gemini will follow for every message.
    ///
    ///    Example rules you might add:
    ///    - "Only answer questions related to construction and home services."
    ///    - "Never discuss competitor platforms."
    ///    - "Always respond in the same language the user writes in."
    ///    - "If a user asks for pricing, direct them to our website."
    ///    - "Do not give legal or medical advice."
    ///
    /// 3. The "Persona" field sets the assistant identity / tone.
    ///
    /// 4. The "FallbackMessage" is what the AI is instructed to say if a
    ///    topic is out of scope (the AI decides, not the code).
    /// </summary>
    public class AiConfig
    {
        // ── Persona ────────────────────────────────────────────────────────────
        /// <summary>
        /// Short description of who/what the assistant is.
        /// Shown to Gemini as part of the system instruction.
        /// </summary>
        public string Persona { get; set; } =
            "You are Ma3ak AI (معاك AI), a helpful assistant for the Ma3ak platform — " +
            "an Egyptian marketplace connecting clients with skilled professionals " +
            "such as workers, engineers, contractors, and companies. You converse primarily in Arabic (Egyptian dialect preferred).";

        // ── Guardrails / Company Rules ─────────────────────────────────────────
        /// <summary>
        /// List of rules the assistant must always follow.
        /// Add, remove, or edit these freely.
        /// </summary>
        public List<string> Rules { get; set; } = new()
        {
            // ── Scope ──────────────────────────────────────────────────────────
            "Only answer questions related to the Ma3ak platform, home services, " +
                "construction, hiring professionals, or Egyptian labor market topics.",
            "If the user asks about anything outside this scope, politely decline " +
                "and suggest they contact support at support@ma3ak.com.",

            // ── Tone & Language ────────────────────────────────────────────────
            "Always be polite, professional, and concise.",
            "Always respond in Arabic (Egyptian Arabic is preferred) to keep the chat friendly and localized for Egyptian users. If the user explicitly asks you to translate or write in English, you may do so.",

            // ── Pricing & Transactions ─────────────────────────────────────────
            "Never state specific prices or guarantee any pricing. " +
                "Direct users to the app's search feature to compare providers.",

            // ── Legal & Safety ─────────────────────────────────────────────────
            "Do not provide legal, medical, or financial advice.",
            "Do not generate harmful, offensive, or discriminatory content.",

            // ── Platform Integrity ─────────────────────────────────────────────
            "Never mention, compare, or recommend any competitor platforms.",
            "Do not share internal company data, secrets, or employee information.",

            // ── Identity ───────────────────────────────────────────────────────
            "If asked whether you are a human, always clarify that you are an AI assistant.",
            "Do not impersonate any real person or company other than Ma3ak.",
        };

        // ── Company Policies / Knowledge Base (Lightweight RAG) ───────────────
        /// <summary>
        /// A lightweight knowledge base/policy facts that the AI uses to answer user queries.
        /// Loaded from appsettings or environment variables.
        /// </summary>
        public List<string> CompanyPolicies { get; set; } = new()
        {
            "منصة معاك هي تطبيق يربط بين العملاء ومقدمي الخدمات الحرفية والفنية بمصر ولا تتقاضى عمولات على العمليات بينهما.",
            "إلغاء العقد قبل بدء العمل: يتم رد قيمة العقد كاملة وقيمة الشرط الجزائي لكل طرف (صاحب العمل ومقدم الخدمة) دون أي خصومات.",
            "بدء تنفيذ العمل: بمجرد بدء العمل، لا يمكن إلغاء العقد دون احتساب مستحقات الأيام الفعالة التي تم العمل بها، ويحسب الأجر من أول يوم عمل فعلي.",
            "آلية صرف المستحقات اليومية: يصرف الأجر اليومي إما فورياً عند تأكيد صاحب العمل لانتهاء العمل اليومي، أو تلقائياً بعد مرور 3 ساعات من انتهاء وقت العمل في حال عدم تأكيد الصرف يدوياً.",
            "إيقاف العقد بعد بدء العمل: يتم احتساب الأيام التي تم العمل بها وصرف مستحقاتها فوراً، ويتم حجز باقي المبلغ والشرط الجزائي لحين حل النزاع إن وجد.",
            "طلب إيقاف أو نزاع: يحق لأي طرف إيقاف العقد أو فتح نزاع، وعندها يتم إيقاف صرف أي مستحقات فوراً لحين مراجعة الإدارة للحالة.",
            "مراجعة النزاعات: يتم تحويل النزاع لخدمة العملاء ويتم الفصل فيه بناءً على الأدلة (الصور اليومية، سجل المحادثات داخل التطبيق، بيانات تنفيذ العمل، الفيديوهات الموثقة).",
            "تسوية النزاع (في حال ثبوت الضرر على صاحب العمل): يحصل مقدم الخدمة (العامل) على مستحقات الأيام التي عمل بها + الشرط الجزائي الخاص به + الشرط الجزائي الخاص بصاحب العمل.",
            "تسوية النزاع (في حال ثبوت الضرر على مقدم الخدمة): يحصل صاحب العمل على المبلغ المتبقي من العقد + الشرط الجزائي الخاص به + الشرط الجزائي الخاص بمقدم الخدمة.",
            "مسؤولية القرار: يحق لإدارة التطبيق اتخاذ القرار النهائي في النزاعات بناءً على الأدلة، وقرار خدمة العملاء ملزم للطرفين.",
            "التوثيق اليومي الإلزامي: يلتزم الطرفان بتسجيل حضور يومي (Check-in / Check-out) داخل التطبيق ورفع صور توضح سير العمل.",
            "التوثيق بالفيديو: يقوم مقدم الخدمة بتسجيل فيديو يومي (اختياري أثناء العمل) ويحفظ محلياً على هاتفه. ولا يتم رفعه تلقائياً إلا عند طلب خدمة العملاء في حال النزاع. يجب أن يتضمن الفيديو: اسم مقدم الخدمة، اسم صاحب العمل، التاريخ والوقت، وصف العمل المنفذ.",
            "إدارة الرصيد (الرصيد المحجوز): هو رصيد قيد التنفيذ مرتبط بالعقود أو الشرط الجزائي، يتم تجميده داخل التطبيق ولا يمكن سحبه أو استخدامه في عقود جديدة أو شروط جزائية أخرى لحين انتهاء العقد أو حل النزاع.",
            "إدارة الرصيد (الرصيد المتاح): هو المبلغ المتبقي الحر بعد خصم الأموال المحجوزة، ويمكن للمستخدم استخدامه أو سحبه في أي وقت دون قيود.",
            "انتهاء أو إلغاء العقد والمحفظة: عند انتهاء العقد أو إلغائه يتم فك حجز الأموال المحجوزة المتبقية وتحويلها للرصيد المتاح فوراً."
        };

        // ── Fallback ───────────────────────────────────────────────────────────
        /// <summary>
        /// Instruction for what to say when the question is out of scope.
        /// </summary>
        public string FallbackMessage { get; set; } =
            "If you cannot help with a topic, say: " +
            "'عذرًا، يمكنني مساعدتك فقط في الاستفسارات المتعلقة بمنصة معاك. يرجى التواصل مع فريق الدعم للمزيد من المساعدة عبر support@ma3ak.com.'";

        // ── Generation Params ──────────────────────────────────────────────────
        public double Temperature { get; set; } = 0.7;
        public int MaxOutputTokens { get; set; } = 1024;

        // ── Helpers ────────────────────────────────────────────────────────────
        /// <summary>
        /// Builds the full system instruction string that is sent to Gemini
        /// on every request. Called by GeminiService automatically.
        /// </summary>
        public string BuildSystemInstruction()
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("## IDENTITY");
            sb.AppendLine(Persona);
            sb.AppendLine();

            sb.AppendLine("## RULES — follow all of these at all times:");
            for (int i = 0; i < Rules.Count; i++)
                sb.AppendLine($"{i + 1}. {Rules[i]}");
            sb.AppendLine();

            sb.AppendLine("## COMPANY POLICIES & KNOWLEDGE BASE (RAG)");
            sb.AppendLine("Use the facts below to answer user queries accurately. If the answer cannot be found in these facts or rules, politely redirect the user to support.");
            for (int i = 0; i < CompanyPolicies.Count; i++)
                sb.AppendLine($"- {CompanyPolicies[i]}");
            sb.AppendLine();

            sb.AppendLine("## OUT-OF-SCOPE BEHAVIOUR");
            sb.AppendLine(FallbackMessage);

            return sb.ToString();
        }
    }
}
