const thumbnailInput = document.getElementById('thumbnail');
if (thumbnailInput.files.length > 0) {
    formData.append('Thumbnail', thumbnailInput.files[0]);
}
