import glob

import torch
import fitz
import io
import os
from PIL import Image
import matplotlib.pyplot as plt
from scripts.ops_colqwen3_embedder import OpsColQwen3Embedder

# 1. Initialize the Hardware-Optimized Embedder
# We use 'sdpa' instead of 'flash_attention_2' for native Windows/4080 Super support
print("Initializing OpsColQwen3Embedder on 4080 Super...")
embedder = OpsColQwen3Embedder(
    model_name="OpenSearch-AI/Ops-Colqwen3-4B",
    dims=2560,
    dtype=torch.float16,
    attn_implementation="sdpa" 
)

all_pages_images = []
metadata = []

# 2. PDF Indexing Phase
print("\n--- Phase 1: Indexing ---")
pdf_files = glob.glob(os.path.join("test_pdfs", "*.pdf"))
if not pdf_files:
    print(f"No PDFs found in \"test_pdfs\"! Check your folder path.")
    exit()

for path in pdf_files:
    print(f"Processing: {os.path.basename(path)}...")
    try:
        with fitz.open(path) as doc:
            for page_num in range(len(doc)):
                page = doc.load_page(page_num)
                # High-res render for the 4080 Super to "see" clearly
                pix = page.get_pixmap(matrix=fitz.Matrix(2, 2)) 
                img = Image.open(io.BytesIO(pix.tobytes("png"))).convert("RGB")
                img.load()
                
                all_pages_images.append(img)
                metadata.append({"file": path, "page": page_num + 1})
    except Exception as e:
        print(f"Could not read {path}: {e}")

# 3. Encoding Phase
print(f"\nEncoding {len(all_pages_images)} pages. This may take a moment...")
# The embedder class handles the <|image_pad|> tokens and .to(device) internally
image_embeddings = embedder.encode_images(all_pages_images)
print(f"Indexing complete. Created {len(image_embeddings)} multi-vector embeddings.")

# 4. Search & Visualization Loop
print("\n--- Phase 2: Search ---")
while True:
    query = input("\nQuery (or Enter to exit): ").strip()
    if not query: 
        break

    # The embedder handles the 'Question:' prefix and MaxSim scoring automatically
    query_embeddings = embedder.encode_queries([query])
    
    # compute_scores returns a list of lists; we take the first (and only) query's scores
    raw_scores = embedder.compute_scores(query_embeddings, image_embeddings)[0]

    # Combine scores, metadata, and images for sorting and visualization
    results = sorted(
        zip(raw_scores, metadata, all_pages_images), 
        key=lambda x: x[0], 
        reverse=True
    )

    print("\nTop Matches:")
    # Prepare visualization for top 2 results
    num_display = min(2, len(results))
    fig, axes = plt.subplots(1, num_display, figsize=(15, 8))
    
    # Handle case where only 1 page is indexed
    if num_display == 1:
        axes = [axes]

    for i in range(num_display):
        score, meta, img = results[i]
        filename = os.path.basename(meta['file'])
        print(f"[{score:.2f}] {filename} - Page {meta['page']}")
        
        axes[i].imshow(img)
        axes[i].set_title(f"Score: {score:.2f}\n{filename} (Pg {meta['page']})")
        axes[i].axis('off')

    plt.tight_layout()
    print("Opening visualization window...")
    plt.show()