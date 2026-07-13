import type {ReactNode} from 'react';
import Link from '@docusaurus/Link';
import Heading from '@theme/Heading';
import styles from './styles.module.css';

type FeatureItem = {
  number: string;
  label: string;
  title: string;
  description: string;
};

const features: FeatureItem[] = [
  {
    number: '01',
    label: 'Design',
    title: 'Build and change workflows faster',
    description:
      'Use the visual editor to refine prompts, models, tools, branches, parallel work, and human checkpoints without rebuilding your orchestration layer.',
  },
  {
    number: '02',
    label: 'Connect',
    title: 'Build on the code you already have',
    description:
      'Run inside ASP.NET Core, call backend services directly, and expose typed C# tools instead of creating a separate integration stack.',
  },
  {
    number: '03',
    label: 'Adapt',
    title: 'Choose the right model for each job',
    description:
      'Use OpenAI, Azure OpenAI, Anthropic, Azure AI Foundry, or Google models and change providers as requirements evolve.',
  },
  {
    number: '04',
    label: 'Recover',
    title: 'Stay available when providers are not',
    description:
      'Configure ordered model fallbacks so eligible rate limits, outages, network failures, and timeouts do not have to end the workflow.',
  },
  {
    number: '05',
    label: 'Understand',
    title: 'Know what happened in every run',
    description:
      'Use node traces, contexts, token and cost metrics, fallback attempts, and OpenTelemetry spans to diagnose behavior with evidence.',
  },
  {
    number: '06',
    label: 'Prove',
    title: 'Make AI quality measurable',
    description:
      'Run repeatable evaluation datasets and grader workflows to compare prompts, models, and design changes before they reach production.',
  },
];

const workflowSteps = [
  {
    number: '1',
    title: 'Design with context',
    description: 'Shape behavior visually and use C# where your domain logic adds the most value.',
  },
  {
    number: '2',
    title: 'Run in your environment',
    description: 'Execute with your services, data, tools, credentials, and security boundaries.',
  },
  {
    number: '3',
    title: 'Improve with evidence',
    description: 'Use traces, metrics, and evaluations to see what changed and whether it helped.',
  },
];

function ArrowIcon(): ReactNode {
  return (
    <svg viewBox="0 0 20 20" aria-hidden="true">
      <path d="M4 10h11M11 6l4 4-4 4" />
    </svg>
  );
}

function Feature({number, label, title, description}: FeatureItem): ReactNode {
  return (
    <article className={styles.featureCard}>
      <div className={styles.featureMeta}>
        <span>{number}</span>
        <span>{label}</span>
      </div>
      <Heading as="h3" className={styles.featureTitle}>
        {title}
      </Heading>
      <p className={styles.featureDescription}>{description}</p>
    </article>
  );
}

export default function HomepageFeatures(): ReactNode {
  return (
    <>
      <section className={styles.featuresSection}>
        <div className={styles.sectionShell}>
          <div className={styles.sectionIntro}>
            <div>
              <p className={styles.sectionEyebrow}>Capabilities</p>
              <Heading as="h2" className={styles.sectionTitle}>
                The speed of visual workflows. The control of code.
              </Heading>
            </div>
            <p className={styles.sectionDescription}>
              Move from experimentation to dependable operation without putting
              a low-code black box between your workflows and the application
              that runs them.
            </p>
          </div>

          <div className={styles.featureGrid}>
            {features.map((feature) => (
              <Feature key={feature.number} {...feature} />
            ))}
          </div>
        </div>
      </section>

      <section className={styles.workflowSection}>
        <div className={styles.workflowShell}>
          <div className={styles.workflowContent}>
            <p className={styles.sectionEyebrow}>A tighter development loop</p>
            <Heading as="h2" className={styles.workflowTitle}>
              Design, run, and learn in one platform.
            </Heading>
            <p className={styles.workflowLead}>
              Keep the people shaping AI behavior close to the code, data, and
              operational feedback that determine whether it succeeds.
            </p>

            <div className={styles.workflowSteps}>
              {workflowSteps.map((step) => (
                <div className={styles.workflowStep} key={step.number}>
                  <span className={styles.stepNumber}>{step.number}</span>
                  <div>
                    <Heading as="h3">{step.title}</Heading>
                    <p>{step.description}</p>
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className={styles.codePanel}>
            <div className={styles.codeHeader}>
              <span>Program.cs</span>
              <span>C#</span>
            </div>
            <pre className={styles.codeBlock}>
              <code>
                <span className={styles.codeMuted}>// Keep workflow execution in your host</span>
                {'\n'}builder.Services
                {'\n'}  .<span className={styles.codeMethod}>AddSharpOMaticEngine</span>()
                {'\n'}  .<span className={styles.codeMethod}>AddSqliteRepository</span>(
                {'\n'}    connectionString);
                {'\n\n'}builder.Services
                {'\n'}  .<span className={styles.codeMethod}>AddSharpOMaticEditor</span>();
                {'\n\n'}app.<span className={styles.codeMethod}>MapSharpOMaticEditor</span>();
              </code>
            </pre>
            <div className={styles.codeFooter}>
              <span>Your runtime. Your services. Your data.</span>
              <span className={styles.codeReady}>Connected</span>
            </div>
          </div>
        </div>
      </section>

      <section className={styles.ctaSection}>
        <div className={styles.ctaPanel}>
          <div className={styles.ctaGlow} aria-hidden="true" />
          <div className={styles.ctaContent}>
            <p className={styles.ctaEyebrow}>Ready to build?</p>
            <Heading as="h2">Shorten the path from idea to production.</Heading>
            <p>
              Add SharpOMatic to your application, connect a model provider, and
              build your first workflow in minutes.
            </p>
          </div>
          <div className={styles.ctaActions}>
            <Link
              className={styles.ctaPrimary}
              to="/docs/getting-started/start-with-nuget-packages">
              Start with NuGet
              <ArrowIcon />
            </Link>
            <Link className={styles.ctaSecondary} to="/docs/">
              Explore the docs
            </Link>
          </div>
        </div>
      </section>
    </>
  );
}
