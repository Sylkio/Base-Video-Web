document.addEventListener("DOMContentLoaded", function () {
    const form = document.getElementById('uploadForm');
    form.addEventListener('submit', async function (event) {
        event.preventDefault();
        const fileInput = document.getElementById('file');
        if (fileInput.files.length === 0) {
            alert('Please select a video file to upload.');
            return;
        }

        const file = fileInput.files[0];
        if (file.size > 200 * 1024 * 1024) {
            alert('Please select a video file smaller than 200MB.');
            return;
        }

        const formData = new FormData(form);
        /*try {
            //alert('Uploading video, please wait...');
            const response = await fetch(form.action, {
                method: 'POST',
                body: formData
            });
            if (response.ok) {
                alert('Video uploaded successfully!');
                window.location.href = '/'; // Redirect to home or any other page
            } else {
                alert('Failed to upload video. Please try again.');
            }
        } catch (error) {
            alert('An error occurred during upload.');
        }*/

        try {
            fetch(form.action, {
                method: 'POST',
                body: formData
            }).then(() => {
                alert('Video uploaded successfully!');
                window.location.href = '/';
            }).catch(error => {
                alert('Failed to upload video. Please try again.');
            });
        } catch (error) {
            alert('An error occurred during upload.');
        }
    });
});
