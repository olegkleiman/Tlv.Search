# Curiosity Project (Tlv.Search)

This document describes the architecture design and some implementation details of Curiosity project - Semantic Search for TLV text corpuses.

The idea behind semantic search is to embed all entries in the corpus into vector space. Embedding usually performed by *Transformer* - the core ML model of modern NLP. Unlike traditional NLP models, which rely on recurrent neural networkd (RNNs) and convolutional neural networks (CNNs), transformers use self-attention mechanism to capture relationships between words in a sentence.

At the search time, the prompt is embedded into the same vector space and the closest embeddings from the corpus are found. These entries should have a high semantic overlap with the query.

![docs similarity](https://raw.githubusercontent.com/UKPLab/sentence-transformers/master/docs/img/SemanticSearch.png)

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

Thus the described approach is heavily based on Azure SQL ability to efficiently calculate the vector dot products for hundreds of docs, actually serving as vector database. From other hand, being relational database, Azure SQL may find the nearest vectors without kNN approximation algorithms, like HNSW ([Hierarchical Navigable Small World](https://en.wikipedia.org/wiki/Small-world_network)) or [FAISS](https://github.com/facebookresearch/faiss).

## Q&A
### What model Curiosity uses?

Curiosity uses OpenAI model [text-embedding-ada-002](https://openai.com/blog/new-and-improved-embedding-model) hosted at MS Azure and deployed in Azure’s meaning of the word.  

### Is it possible to use other NLP models with Curiosity?

There is a pletora of pre-trained models at HuggingFace hub. The models used for embeddings are called there “features extraction”.  It should be noted that inference API that proposed by HuggingFace may not be used at production grade. There is payed professional services that HuggingFace provides for production solutions.

### Curiosity in fact Microsoft-locked solution. Is it possible to unlock it from MS Azure and OpenAI?

Actually Curiosity may use other models than OpenAI as described in previous section. Changing Azure SQL is more challenging task because, as mentioned, Curiosity stores the docs corpus and corresponding embeddings vectors in SQL relational tables. Moreover, when calculating docs similarity, Azure SQL performs ordering sort instead of kNN approximation algorithm. 

1. The most prominent candidate for an alternative of Azure OpenAI is [HuggingFace Inference API](https://huggingface.co/docs/api-inference/index) with some BERT-based model. Considering the following code excertp:
``` JS
import { HfInference } from '@huggingface/inference';
import pkg from 'hnswlib-node';
const { HierarchicalNSW } = pkg;

const model_id = "sentence-transformers/all-MiniLM-L6-v2"
const hf_token = "ht_De...";

const texts = ["What is most most prominent nlp model",
"Who created BERT model",
...
"What is BERT architecture"];

// Raw requests to inference API imply calculating similary without help of HuggingFace.
// We use HNSW here for this purpose
const api_url = `https://api-inference.huggingface.co/pipeline/feature-extraction/${model_id}`; 
let response = axios.post(api_url,
          {
            "inputs": texts,
            "options": {"wait_for_model": 1}
          }, {
            headers: {
              "Authorization": `Bearer ${hf_token}`
            }
          }
 const corpus_embeddings = response.data;

 const numDimensions = 384; // this is number for MiniLM-L6-v2 model

 const index = new HierarchicalNSW('cosine', numDimensions);
 index.initIndex(maxElements);

 for(let i = 0; i < corpus_embeddings.length; i++) {
     index.addPoint(corpus_embeddings[i], i);
 }

 const numNeighbors = 3;
 response = index.searchKnn(query_embedding, numNeighbors);
 // {
 //   distances: [0.66, 0.67, 0.71543],
 //   neighbors: [5,8,9]
 // }

// OR

const inference = new HfInference(hf_token);
let res = await inference.sentenceSimilarity({
   model: model_id,
   inputs: {
      source_sentence: "What is BERT?",
      sentences: texts
   }
})
// [0.605, 0.561, 0.7345, ... ,0.344] - distances from each doc passed in 'sentences' param
   
   ``` 

2. Obviously some vector database may be considered as alternative to Azure SQL with the table with columnstore index and Node.js could be alternative to Azure-based functions. Consider the following JS in Node.js that uses [HNSW](https://github.com/yoshoku/hnswlib-node) as vector database used for docs similarity
``` JS
import pkg from 'hnswlib-node';
const { HierarchicalNSW } = pkg;
...
const index = new HierarchicalNSW('cosine', numDimensions);
index.initIndex(maxElements);
for(var i = 0; i < corpus_embeddings.length; i++) {
  index.addPoint(corpus_embeddings[i], i);
}
// Store embeddings into file
index.writeIndexSync('curiosity.dat');

```
3. ElasticSearch
   Starting with version 7.3, ElasticSearch introduces the possibility to index dense vectors and to use it for docs scoring. Hence, we can use ElasticSearch to index embeddings along the docs and we can use the query embeddings to retrieve relevant entries. ES index configuration may looks like:
``` JSON

"mappings": {
          "properties": {
                    "Embeddings": {
                              "type": "dense_vector",
                              "dims": 512,
                              "index": True,
                              "similarity": "cosine"
                    }
          }
}
```
configurations["settings"]
