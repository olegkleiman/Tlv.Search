# Curiosity Project (Tlv.Search)

This document describes the architecture design and some implementation details of Curiosity project - Semantic Search for TLV text corpuses.

The idea behind semantic search is to embed all entries in the corpus into vector space. Embedding is usually performed by *Transformer* - the core ML model of modern NLP. Unlike traditional NLP models, which rely on recurrent neural networks (RNNs) and convolutional neural networks (CNNs), transformers use a self-attention mechanism to capture relationships between words in a sentence.

The prompt is embedded into the same vector space at the search time and the closest embeddings from the corpus are found. These entries should have a high semantic overlap with the query.

![docs similarity](https://raw.githubusercontent.com/UKPLab/sentence-transformers/master/docs/img/SemanticSearch.png)

The mathematical basis of the solution is the vector distance computation performed for the embedded prompt of the user and (large) corpus of the embedded documents previously pre-processed for this purpose. 

The first phase of the whole solution is docs ingesting. It is a relatively complex procedure consisting of extracting the text from someplace (SharePoint in our case), cleaning it up (standardization), tokenizing and finally embedding the text. This phase is performed when the content of the docs corpus is changed or updated. i.e. by relatively long intervals (once per day or even rarely).
Embedding is the most important operation here. Simply put, it’s transforming the text into the real-numbers vector, some kind of vectorization, where the produced vector pretends to represent the semantic meaning of the sentence. 
Embeddings are derived from extensive text data using techniques like Transformer-based models such BERT or GPT; GPT’s Embeddings API and corresponding Azure OpenAI services serve this purpose.

As said, docs ingestion procedure is executed by demand once there are updates in the docs corpus.
The sub-project launched for the implementation of this procedure is called Phoenix. In its current state, it is a Windows executable invoked manually and performing the following steps:
- looking for the configuration table in Curiosity Azure SQL, where it finds the list of the URLs for different SharePoint lists. Each list is pulled and its content is inserted into *site_docs* table. The title and the original URL are also stored for further use.
- Each processed list consists of several texts. Any such text is embedded with the help of an AI model and the obtained vector is inserted into *site_vector_docs* table. Assuming the model produces 1500 float numbers for each doc, the row count in this table will be 1500*(number of docs in list)*(number of lists)
As described, the output of this preliminary procedure is two filled tables in SQL Azure: *site_docs* and *site_vector_docs*. (Note the columnstore index at *site_vector_docs* table)

Prompt processing.
Assuming the text corpus embedding were produced successfully and stored as float vectors in Azure SQL table with columnstore index, now it is possible to process the user’s prompt. Actually, this is done by Azure Function with HTTP trigger that invoked (with GET invocation) after the user requests the search from the site.

This function connects to Azure SQL Server and invokes the stored procedure - CalculateDistance - that performs embeddings for user’s prompt and calculates the cosine distance between it and the text corpus. It orders the calculated distances by descending order and returns top 5 results joined with site_docs table for docs metadata: url, title, etc.
Prompt embedding within the stored procedure is HTTP invocation of the deployed model with the help of modern SQL stored procedure sp_invoke_external_rest_endpoint to the endpoint exposed by Azure OpenAI service. This way is also eliminates passing the string with embedding text representation into the stored procedure. Actually, it receives the prompt text and gets the embeddings by itself.

Thus, the described approach is heavily based on Azure SQL ability to efficiently calculate the vector dot products for hundreds of docs, actually serving as vector database. From other hand, being relational database, Azure SQL may find the nearest vectors without kNN approximation algorithms, like HNSW ([Hierarchical Navigable Small World](https://en.wikipedia.org/wiki/Small-world_network)) or [FAISS](https://github.com/facebookresearch/faiss).

## Q&A
### What model Curiosity uses?

Primarily, Curiosity uses OpenAI embedding model [text-embedding-ada-002](https://openai.com/blog/new-and-improved-embedding-model) hosted at MS Azure and deployed into corresponding subscription.

### Is it possible to use other NLP models with Curiosity?

There is a pletora of pre-trained models at HuggingFace hub. The models used for embeddings are listed there [“features extraction”](https://huggingface.co/models?pipeline_tag=feature-extraction&sort=trending). We've tried several models on pre-defined doc corpus. The most appreciable results were obtained from [multi-qa-MiniLM-L6-cos-v1](https://huggingface.co/sentence-transformers/multi-qa-MiniLM-L6-cos-v1), [msmarco-distilbert-base-tas-b](https://huggingface.co/sentence-transformers/msmarco-distilbert-base-tas-b), [all-MiniLM-L6-v2](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2) and [full-sentence-distillroberta3](https://huggingface.co/osanseviero/full-sentence-distillroberta3). The example of using the first of them may be found below.

It should be noted that inference API that proposed by HuggingFace may barely be used at production grade. There is payed professional services that HuggingFace provides for production solutions.

### Curiosity Project based on embeddings produced by the model. Can this approach be used for docs classification?

Classification based on embeddings is popular technique in NLP. Practically, embeddings can be used for zero-shot classification. For each class, embed the class name or short description of the class. To classify some new text in a zero-shot manner, compare its embedding to all class embeddings and predict the class with the highest similarity.
``` Python
import openai
from openai.embeddings_utils import cosine_similarity, get_embedding
from sentence_transformers import SentenceTransformer, util

openai.api_key = "sk-..."

labels = ["sport", "math", "cinema", "books", "freemasons"]
model_name = "text-embedding-ada-002"
transformer_name = "sentence-transformers/multi-qa-MiniLM-L6-cos-v1"
texts = [
    "Founded in 1910, Sport Club Corinthians Paulista is a Brazilian sports club based in São Paulo. It is considered "
    "one of the most successful and popular football teams in Brazil, boasting a large fanbase known as \"Fiel\" ("
    "Faithful). \n\nCorinthians has won the Brazilian Serie A (the top tier of Brazilian football) seven times, "
    "and has also claimed the Copa do Brasil (the Brazilian domestic cup) three times. Internationally, the team has "
    "won the FIFA Club World Cup twice, in 2000 and 2012, showcasing its place on the world stage of football.\n\nIn "
    "addition to football, the Corinthians club also has departments for other sports, such as futsal, swimming, "
    "and esports. The club’s home matches are played at Arena Corinthians, which was opened in 2014 and also hosted "
    "matches during the 2014 FIFA World Cup.\n\nThe club is named after the English amateur team Corinthians Casuals, "
    "which was known for promoting the principles of Fair Play.",
    "Fermat's Last Theorem states that no three positive integers a, b, and c can satisfy the equation a^n + b^n = "
    "c^n for any integer value of n greater than 2. This theorem was first conjectured by Pierre de Fermat in 1637, "
    "but a proof was not found until 1994 by the British mathematician Andrew Wiles",
    "Here begin the constitutions of the art of Geometry according to Euclid. Whoever will both well read and look He "
    "may find written in old book Of great lords and also ladies, That had many children together, certainly; And had "
    "no income to keep them with, Neither in town nor field nor enclosed wood;",
    "And all the points herein before To all of them he must be sworn, And all shall swear the same oath Of the "
    "masons, be they willing, be they loth"]

label_embeddings = [(label, get_embedding(label, engine=model)) for label in labels]

def predictClass(text):
         text_embedding = get_embedding(text, engine=model_name)
         similarities = [cosine_similarity(text_embedding, label_embedding[1]) for label_embedding in label_embeddings]
         maxSimilarity = max(similarities)
         label_index = similarities.index(maxSimilarity)
         return label_embeddings[label_index][0], maxSimilarity

def predict_class_with_sentence_transformer(text):
        transformer = SentenceTransformer(transformer_name)
        labels_emb = transformer.encode(labels)
        query_emb = transformer.encode(text)
        similarities = util.dot_score(query_emb, labels_emb)[0].cpu().tolist()
        max_similarity = max(similarities)
        label_index = similarities.index(max_similarity)
        return label_embeddings[label_index][0], max_similarity

query = texts[3]

res = predictClass(query)
print(res)

res = predict_class_with_sentence_transformer(query)
print(res)

```

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
configurations["settings"]
```
In this example Embeddings are produced by the model (universal-sentense-decoder) hosted at TensorFlow Hub:
``` Python
import tensorflow_hub as hub

embed = hub.load("https://tfhub.dev/google/universal-sentence-encoder/4")
embeddings = embed([
"The quich brown fox",
"I am a sentence for which I would like to get its embeddings"])

print(embeddings)

# Compute similarity matrix. Higher score indicates greater similarity.
similarity_matrix_it = np.inner(en_result, it_result)
```

Then new index could be created and filled out (Python)
``` Python
from elasticsearch import es

          actions = []
          index_name = "site_docs_vector"
          es.indices.create(index=index_name,
                              settings=configuration["settings"],
                              mappings=configuration["mappings"]
                              )
          for index, row in df.iterrows():
                    action = {"index": {"_index": index_name, "_id": index} }
                    doc = {
                              "id": index,
                              "Text": row["text"],
                              "Embeddings": row["Embeddings"]
                    }
                    actions.append(action)
                    actions.append(doc)

          es.bulk(index=index_name, operations=actions)
                    
```
Next, given the embeddings for user's query, the search is done as:
``` Python
query_for_search = {
          "knn": {
                    "field": "Embeddings",
                    "query_vector": query_vector,
                    "k": 5,
                    "num_candidates": 2414
          },
          "_source": ["Text"]
}
result = es.search(
          index=index_name,
          body=query_for_search)
result["hits"]
```
ES natively supports K-Nearest Neighbors algorithm and it do not be trained separately.
