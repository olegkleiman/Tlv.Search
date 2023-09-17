# Curiosity Project (Tlv.Search)

This document describes the architecture design and some implementation details of Curiosity project - Smart Search for TLV text corpuses.

The mathematical basis of the solution is the vector distance computation performed for the embedded prompt of the user and (large) corpus of the embedded documents previously pre-processed for this purpose. 

The first phase of the whole solution is docs ingesting. It is a relatively complex procedure consists of the text extracting from some place (SharePoint in our case), cleaning it up (standardization), tokenization  and finally embedding the text. This phase is performed when the content of the docs corpus is changed or updated. i.e. by relatively long intervals (once per day or even rarely).
Embedding is the most important operation here. Simply put, it’s transforming the text into real-numbers vector, some kind of vectorization, where the produced vector pretends to represent the semantic meaning of the sentence. 
Embeddings are derived from extensive text data using techniques like Transformer-based model as BERT or GPT; GPT’s Embeddings API and corresponding Azure OpenAI services serve this purpose.

As said, docs ingestion procedure is executed by demand once there are the updates in the docs corpus.
The sub-project launched for the implementation of this procedure is called Phoenix. In its current state it is Windows executable invoked manually and preforming the following steps:
- looking for the configuration table in Curiosity Azure SQL, where it finds the list of the URLs for different SharePoint lists. Each list is pulled and its content is inserted into site_docs table. The title and the original url are also stored for further uses.
- Each processed list consists of a number of texts. Any such text is embedded with a help of AI model and the obtained vector is inserted into site_vector_docs table. Assuming the model produces 1500 float numbers for each doc, the rows count in this table will be 1500*(number of docs in list)*(number of lists)
As described, the output of this preliminary procedure is two filled tables in SQL Azure: site_docs and site_vector_docs. (Note the columnstore index at site_vector_docs table)

Prompt processing.
Assuming the text corpus embedding were produced successfully and stored as float vectors in Azure SQL table with columnstore index, now it is possible to process the user’s prompt. Actually, this is done by Azure Function with HTTP trigger that invoked (with GET invocation) after the user requests the search from the site.

This function connects to Azure SQL Server and invokes the stored procedure - CalculateDistance - that performs embeddings for user’s prompt and calculates the cosine distance between it and the text corpus. It orders the calculated distances by descending order and returns top 5 results joined with site_docs table for docs metadata: url, title, etc.
Prompt embedding within the stored procedure is HTTP invocation of the deployed model with the help of modern SQL stored procedure sp_invoke_external_rest_endpoint to the endpoint exposed by Azure OpenAI service. This way is also eliminates passing the string with embedding text representation into the stored procedure. Actually it receives the prompt text and gets the embeddings by itself.

Thus the described approach is heavily based on Azure SQL ability to efficiently calculate the vector dot products for hundreds of docs, actually serving as vector database. From other hand, being relational database, Azure SQL may find the nearest vectors without kNN approximation algorithms, like HNSW ([Hierarchical Navigable Small World](https://en.wikipedia.org/wiki/Small-world_network) or [FAISS](https://github.com/facebookresearch/faiss).

Q&A
### What model Curiosity uses?

Curiosity uses OpenAI model text-embedding-ada-002 hosted at MS Azure and deployed in Azure’s meaning of the word.  

  - list A
  - list B

### Is it possible to use other NLP models with Curiosity?

- There is a pletora of pre-trained models at HuggingFace hub. The models used for embeddings are called there “features extraction”.  It should be noted that inference API that proposed by HuggingFace may not be used at production grade. There is payed professional services that HuggingFace provides for production solutions.
- Curiosity in fact Vendor-locked solution. Is it possible to unlock it from MS Azure and OpenAI?
- Actually Curiosity may use other models than OpenAI as described in previous section. It the model 

