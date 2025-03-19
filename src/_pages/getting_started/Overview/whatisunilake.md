---
title: What is Unilake?
---

Unilake is an open source, fast, scalable, user-friendly **Unified Data Analytics Platform for data and AI**. Run Unilake where you want, how you want and with whatever changes you need to excel in your data and AI journey. Welcome to the freedom of choice.

## Introducing Unilake
Introducing Unilake, a comprehensive, fully open-source data and AI  platform designed to address every aspect of your organization's  data-driven projects in an integrated and holistic manner. Our platform features an opinionated architecture that simplifies adoption and enables rapid deployment. With cutting-edge technologies and best  practices in areas such as data integration, analysis, productization, sharing, and governance, Unilake empowers businesses to unlock valuable insights from their data estates. Our commitment is to become the go-to open-source data and AI platform accessible to business, research projects and governments of all sizes and budgets.

## Unified Data Analytics Platform
Unilake consists only of fully open source components. From its control plane, compute plane to selecting a data plane to host your data, you can fully self-host the complete platform and all of its features are made open source. We specifically focus on the following aspects for building our data platform:

- **Performance:** Unilake leverages both StarRocks lakehouse engine for SQL compute and the Daft dataframe technology for increased performance.
- **Scalability:** Whether this is technological scalability (adding extra compute) as well as organizational scalability (adding additional teams), Unilake is designed to accomodate incidental spikes of compute as well as an increase of teams without additional complexity
- **Security:** When it comes to making data available we apply our own security layer to allow for different compute engines to apply on-the-fly ABAC security controls, making sure data is secured no matter what engine you choose
- **Open:** Unilake and its components are fully open source

### What makes Unilake unified?
Unilake focuses on all core aspects of a data platform, this covers: data integration (batch and streaming), security and governance, data modelling and analyses, data sharing and productization, applying data science such as AI or ML to making data available for downstream consumption (Business Intelligence or customer facing analytics). This allows Unilake to be your organisational one-stop shop for data and analytics initiatives, enabling quicker access to data and faster iterations of ideas.

## Architecture
Unilake follows a highly opinionated and decoupled architecture, allowing for a best-in-class set of technologies to work together. The whole of its components encompasses everything you need to get your data and AI ambitions of the ground. While different platforms will have its unique strengths and weaknesses, we will not constrain the implementation of Unilake to just its own components allowing you to mix and match use cases between the use of Unilake and other platforms.

![Alt text](/img/docs/platformoverview.png) 

The following layers explained:

### Control Plane
The control layer focuses on the management of the platform. It comes with a CLI to provision the platform and manage it. This allows for automation to quickly onboard new teams and allow for easy setup of new environments. Integrate different authentication providers, user and group provisioning and audit logging.

Unilake has one webinterface for accessing any of the different workspaces that you create as workspaces are virtual. This allows for easy switching and compute sharing to lower costs where needed. You can have one compute instance and virtually create new warehouses for each workspace, sharing idle compute resources to better optimize costs. The orchestration of all these things are done in the control plane.

### Compute Plane 
The Compute Plane serves as the primary hub for executing computational tasks. It encompasses two main areas: SQL endpoints utilized for Business Intelligence (BI) and customer-facing analytics, and data science initiatives aimed at generating innovative data models employing Artificial Intelligence (AI) and Machine Learning (ML) techniques. To ensure comprehensive data protection within this unified framework, Unilake has created an advanced security layer known as UniSecure. This robust feature enables you to implement Attribute-Based Access Control (ABAC), thereby facilitating a highly scalable solution from both technical and organizational perspectives. By leveraging these capabilities, users can maintain stringent control over their data while benefiting from seamless integration across various applications and services.

### Data Plane 
Unilake is highly compatibility with diverse storage technologies, spanning various file formats such as Parquet, Optimized Row Columnar (ORC), Comma Separated Values (CSV), JavaScript Object Notation (JSON), multimedia files (images, videos, audio), text documents, Portable Document Format (PDF), among others. Furthermore, it supports multiple table formats including Iceberg, Delta, Apache Hudi, Paimon, and Kudu, along with popular storage systems like Amazon S3, Microsoft Azure Data Lake Storage, Google Cloud Storage, and Hadoop File System (HDFS).

Regardless of your infrastructure setup—whether utilizing a public cloud, private cloud, or an on-premises solution—Unilake is adaptable by integrating into numerous environments under varying conditions. Its support for open table formats empowers seamless interoperability with other technologies and data platforms, enabling organizations to create solutions tailored to their specific needs, free from limitations imposed by proprietary standards. Consequently, businesses can capitalize on the benefits offered by a best-in-class ecosystem, ensuring optimal performance, flexibility, and cost efficiency.

Unilake and its components are fully open-source, fostering transparency, collaboration, and innovation. Unilake operates under the dual license model comprising the Affero General Public License version 3 (AGPLv3) and European Union Public Licence version 1.2 (EUPL 1.2). These widely recognized licenses not only promote community engagement but also guarantee unrestricted access to Unilake's source code, empowering developers worldwide to contribute, modify, and distribute derivative works freely. As a result, Unilake remains committed to upholding the core values of openness, inclusivity, and continuous improvement inherent in the open-source ethos.
